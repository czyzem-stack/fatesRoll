using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>NavMesh travel after dice rolls — purple full route + yellow roll segment when movement starts.</summary>
[DisallowMultipleComponent]
[AddComponentMenu("FatesRoll/Steve Movement")]
public class SteveMovement : MonoBehaviour
{
    private NavMeshAgent agent;
    private LineRenderer rollPathLine;
    private LineRenderer targetPathLine;
    private SteveAnimator steveAnim;

    private bool isMoving;
    private int lastRollValue;
    private int nextPoiOrder;
    private GameObject currentTarget;
    private GameObject lockedTarget;
    private GameObject routeTarget;
    private Enemy approachingEnemy;
    private Enemy approachingChest;
    private HeroController hero;
    private NavMeshPath navPathScratch;
    private readonly List<Vector3> pathPointScratch = new List<Vector3>(32);
    private float lastTargetPathRefreshTime;
    private static Material sharedPathLineMaterial;

    private const float TargetPathRefreshMinInterval = 0.35f;

    public bool IsMoving => isMoving;
    public Enemy ApproachingEnemy => approachingEnemy;
    public int LastRollValue => lastRollValue;
    /// <summary>Next expected <see cref="POINode.order"/> for visit-path targeting.</summary>
    public int NextVisitPoiOrder => nextPoiOrder;

    private void Awake()
    {
        navPathScratch = new NavMeshPath();
    }

    public void Initialize(NavMeshAgent navAgent, SteveAnimator animator)
    {
        agent = navAgent;
        steveAnim = animator;
        hero = GetComponent<HeroController>();
        if (navPathScratch == null)
            navPathScratch = new NavMeshPath();
        EnsurePathLines();
    }

    public void SetNextPoiOrder(int order) => nextPoiOrder = order;

    public void NotifyPoiDefeated(POINode node)
    {
        EndRoute();
        if (node == null)
            return;

        nextPoiOrder = Mathf.Max(nextPoiOrder, node.order + 1);
        if (GameServices.TryGet(out POIManager poiManager))
            poiManager.TryEnableRandomVisitTargeting();
    }

    /// <summary>Stops showing the route to the current POI (arrival, combat start, reset).</summary>
    public void EndRoute()
    {
        currentTarget = null;
        approachingEnemy = null;
        approachingChest = null;
        lockedTarget = null;
        routeTarget = null;
        isMoving = false;
        HideAllPaths();
    }

    public Enemy GetPendingCombatEnemy()
    {
        if (approachingEnemy != null)
            return approachingEnemy;

        Enemy enemy = GetEnemyFromTarget(currentTarget);
        return enemy != null && enemy.IsTreasureChest ? null : enemy;
    }

    public void RecordRoll(int total) => lastRollValue = total;

    public void Stop()
    {
        isMoving = false;

        if (agent == null || !agent.isOnNavMesh)
            return;

        agent.isStopped = true;
        agent.ResetPath();
        agent.velocity = Vector3.zero;
        steveAnim?.SetSpeed(0f);
    }

    /// <summary>One dice segment finished; keep purple route to the POI if still en route.</summary>
    public void OnRollSegmentEnded()
    {
        isMoving = false;
        HideRollPathOnly();
        RefreshTargetPathLine(force: true);
        steveAnim?.SetSpeed(0f);

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
    }

    public void ResetAll()
    {
        Stop();
        EndRoute();
        lastRollValue = 0;
    }

    /// <summary>Travel toward the next POI up to dice distance; draws yellow path when movement begins.</summary>
    public void MoveAfterRoll(int diceTotal)
    {
        lastRollValue = diceTotal;
        GlobalSettings settings = GlobalSettings.Instance;
        if (settings == null || agent == null)
            return;

        if (!agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                agent.Warp(hit.position);
            else
                return;
        }

        if (!PickTarget(out GameObject target))
        {
            GlobalSettings.LogGameplayWarning("SteveMovement: no POI target available for movement.");
            return;
        }

        GlobalSettings.LogGameplay($"Moving toward POI: {target.name}");

        lockedTarget = target;
        routeTarget = target;
        Enemy enemy = GetEnemyFromTarget(target);
        bool isChest = enemy != null && enemy.IsTreasureChest;
        Vector3 pathGoal = isChest
            ? GetChestPathGoal(target, enemy)
            : enemy != null
                ? enemy.GetMeleeApproachPoint(transform.position)
                : target.transform.position;

        if (!NavMesh.CalculatePath(transform.position, pathGoal, NavMesh.AllAreas, navPathScratch) ||
            navPathScratch.status == NavMeshPathStatus.PathInvalid)
            return;

        float pathDist = PathLength(navPathScratch);
        float totalMeters = diceTotal * settings.stepsPerDiceValue * settings.metersPerStep;
        float moveMeters = Mathf.Min(totalMeters, pathDist);
        Vector3 destination = PointOnPath(navPathScratch, moveMeters);

        if (enemy != null && !isChest)
        {
            float distToEnemy = HorizontalDistance(enemy.GetEngagePosition());
            float engage = settings.meleeEngageDistance;
            if (moveMeters < 0.5f && distToEnemy > engage + 0.15f)
            {
                Vector3 toEnemy = enemy.GetEngagePosition() - transform.position;
                toEnemy.y = 0f;
                if (toEnemy.sqrMagnitude > 0.01f)
                {
                    float closeMeters = Mathf.Min(totalMeters, Mathf.Max(0.5f, distToEnemy - engage));
                    destination = transform.position + toEnemy.normalized * closeMeters;
                    moveMeters = closeMeters;
                }
            }
        }

        if (NavMesh.SamplePosition(destination, out NavMeshHit destHit, 2f, NavMesh.AllAreas))
            destination = destHit.position;

        if (enemy != null && moveMeters < 0.15f)
        {
            if (isChest)
            {
                if (hero != null && hero.IsWithinChestInteractRange(enemy))
                {
                    isMoving = false;
                    hero.OnMovementArrivedAtTreasureChest(enemy);
                    return;
                }

                // Path budget left but nav can't advance this frame — end segment (don't spin RefreshTargetPathLine).
                if (pathDist > GlobalSettings.GetChestInteractDistance() + 0.5f)
                {
                    moveMeters = Mathf.Min(totalMeters, Mathf.Max(0.5f, pathDist));
                    destination = PointOnPath(navPathScratch, moveMeters);
                    if (NavMesh.SamplePosition(destination, out NavMeshHit retryHit, 2f, NavMesh.AllAreas))
                        destination = retryHit.position;
                }
                else
                {
                    EndRollSegmentKeepChestRoute(enemy);
                    return;
                }
            }
            else if (IsInMeleeRange(enemy, 0.35f))
            {
                isMoving = false;
                hero?.OnMovementArrivedAtEnemy(enemy);
                return;
            }
        }

        approachingChest = isChest ? enemy : null;
        approachingEnemy = isChest ? null : enemy;
        agent.speed = settings.heroTravelSpeed;
        agent.stoppingDistance = isChest ? GameConstants.ChestPathStoppingDistance 
            : enemy != null ? GameConstants.EnemyMeleeStoppingDistance 
            : GameConstants.EnemyPathStoppingDistance;
        agent.isStopped = false;
        agent.updateRotation = true;

        DrawRollPath(navPathScratch, moveMeters);
        DrawTargetPath(navPathScratch);

        if (!agent.SetDestination(destination))
            return;

        isMoving = true;
        steveAnim?.SetSpeed(0f);

        if (enemy != null)
        {
            string label = string.IsNullOrEmpty(enemy.name) ? "enemy" : enemy.name;
            CombatLog.Info($"Steve moving toward {label} ({moveMeters:F1}m)");
        }
    }

    public void Tick(bool blockTravel)
    {
        if (!isMoving || blockTravel || agent == null)
        {
            if (blockTravel && isMoving)
                steveAnim?.SetSpeed(0f);
            return;
        }

        UpdateWalkAnim();

        Enemy chest = ResolveApproachingChest();
        if (chest != null)
        {
            if (HasReachedChestDestination(chest, hero))
            {
                FinishMove();
                hero?.OnMovementArrivedAtTreasureChest(chest);
                return;
            }

            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + GameConstants.DistanceTolerance)
                FinishMove();

            return;
        }

        Enemy enemy = approachingEnemy ?? GetEnemyFromTarget(currentTarget);
        if (enemy != null && !enemy.IsTreasureChest)
        {
            if (enemy.IsWithinEngageRange(hero))
            {
                FinishMove();
                hero?.OnMovementArrivedAtEnemy(enemy);
                return;
            }

            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + GameConstants.DistanceTolerance)
            {
                FinishMove();
                hero?.OnMovementArrivedAtEnemy(enemy);
            }

            return;
        }

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            GameObject target = currentTarget ?? routeTarget;
            var poiEnemy = GetEnemyFromTarget(target);
            if (poiEnemy != null && poiEnemy.IsTreasureChest)
                return;

            FinishMove();
            if (target != null)
            {
                var poi = target.GetComponent<POINode>() ?? target.GetComponentInParent<POINode>();
                hero?.OnMovementArrivedAtPoi(poi, target);
            }
        }
    }

    private void UpdateWalkAnim()
    {
        if (steveAnim == null || agent == null)
            return;

        if (agent.pathPending || !agent.hasPath)
        {
            steveAnim.SetSpeed(0f);
            return;
        }

        if (agent.remainingDistance <= agent.stoppingDistance + GameConstants.DistanceTolerance)
        {
            steveAnim.SetSpeed(0f);
            return;
        }

        Vector3 vel = agent.velocity;
        vel.y = 0f;
        float speed = vel.magnitude;
        float animSpeed = speed < 0.15f ? 0f : Mathf.Clamp(speed / 3.5f, 0.5f, 2f);
        steveAnim.SetTravelSpeed(animSpeed);
    }

    private void FinishMove()
    {
        if (!isMoving)
            return;
        OnRollSegmentEnded();
    }

    private void EndRollSegmentKeepChestRoute(Enemy chest)
    {
        OnRollSegmentEnded();
        approachingChest = chest;
        approachingEnemy = null;
    }

    private Enemy ResolveApproachingChest()
    {
        if (approachingChest != null && approachingChest.IsTreasureChest)
        {
            if (approachingChest.isDead)
            {
                approachingChest = null;
                return null;
            }

            return approachingChest;
        }

        Enemy onTarget = GetEnemyFromTarget(routeTarget ?? currentTarget);
        if (onTarget != null && onTarget.IsTreasureChest && !onTarget.isDead)
        {
            approachingChest = onTarget;
            approachingEnemy = null;
            return onTarget;
        }

        return null;
    }

    private void RefreshTargetPathLine(bool force = false)
    {
        if (!ShowPaths || routeTarget == null || !IsTargetUsable(routeTarget))
        {
            HideTargetPath();
            return;
        }

        if (!force && Time.time - lastTargetPathRefreshTime < TargetPathRefreshMinInterval)
            return;

        Enemy enemy = GetEnemyFromTarget(routeTarget);
        bool isChest = enemy != null && enemy.IsTreasureChest;
        Vector3 pathGoal = isChest
            ? GetChestPathGoal(routeTarget, enemy)
            : enemy != null
                ? enemy.GetMeleeApproachPoint(transform.position)
                : routeTarget.transform.position;

        if (!NavMesh.CalculatePath(transform.position, pathGoal, NavMesh.AllAreas, navPathScratch) ||
            navPathScratch.status == NavMeshPathStatus.PathInvalid ||
            navPathScratch.corners.Length < 2)
        {
            HideTargetPath();
            return;
        }

        lastTargetPathRefreshTime = Time.time;
        DrawTargetPath(navPathScratch);
    }

    private bool PickTarget(out GameObject target)
    {
        target = null;

        if (GameServices.TryGet(out POIManager poiMgr) && poiMgr.HasRemainingVisitPOI())
            ClearStaleSpawnEncounterTargets();

        if (IsTargetUsable(routeTarget))
        {
            target = routeTarget;
            currentTarget = target;
            AssignApproachFromTarget(target);
            return true;
        }

        if (isMoving && lockedTarget != null && IsTargetUsable(lockedTarget))
        {
            target = lockedTarget;
            routeTarget = target;
            currentTarget = target;
            AssignApproachFromTarget(target);
            return true;
        }

        if (!IsTargetUsable(currentTarget))
            currentTarget = null;

        if (currentTarget == null && GameServices.TryGet(out POIManager poiManager))
            currentTarget = poiManager.GetNextPOITarget(nextPoiOrder);

        target = currentTarget;
        if (target != null)
            routeTarget = target;

        return target != null;
    }

    void ClearStaleSpawnEncounterTargets()
    {
        bool UnderSpawnNode(GameObject go) =>
            go != null && go.GetComponentInParent<SpawnNode>() != null;

        if (UnderSpawnNode(routeTarget)) routeTarget = null;
        if (UnderSpawnNode(lockedTarget)) lockedTarget = null;
        if (UnderSpawnNode(currentTarget)) currentTarget = null;

        approachingEnemy = approachingEnemy != null && UnderSpawnNode(approachingEnemy.gameObject) ? null : approachingEnemy;
        approachingChest = approachingChest != null && UnderSpawnNode(approachingChest.gameObject) ? null : approachingChest;
    }

    private bool IsTargetUsable(GameObject target)
    {
        if (target == null)
            return false;

        var enemy = GetEnemyFromTarget(target);
        if (enemy != null)
            return !enemy.isDead;

        var poi = target.GetComponent<POINode>() ?? target.GetComponentInParent<POINode>();
        return poi != null;
    }

    private static Enemy GetEnemyFromTarget(GameObject target)
    {
        if (target == null)
            return null;
        return target.GetComponent<Enemy>()
               ?? target.GetComponentInChildren<Enemy>()
               ?? target.GetComponentInParent<Enemy>();
    }

    private void AssignApproachFromTarget(GameObject target)
    {
        Enemy enemy = GetEnemyFromTarget(target);
        if (enemy != null && enemy.IsTreasureChest)
        {
            approachingEnemy = null;
            approachingChest = enemy.isDead ? null : enemy;
            return;
        }

        approachingChest = null;
        approachingEnemy = enemy != null && enemy.isDead ? null : enemy;
    }

    private static Vector3 GetChestPathGoal(GameObject target, Enemy chest)
    {
        if (chest != null)
            return chest.GetChestInteractPosition();
        return target != null ? target.transform.position : Vector3.zero;
    }

    private bool HasReachedChestDestination(Enemy chest, HeroController hero)
    {
        if (chest == null || chest.isDead || hero == null || agent == null)
            return false;

        if (!hero.IsWithinChestInteractRange(chest))
            return false;

        if (agent.pathPending)
            return false;

        return agent.remainingDistance <= agent.stoppingDistance + GameConstants.DistanceTolerance;
    }

    private float HorizontalDistance(Vector3 worldPos)
    {
        Vector3 a = transform.position;
        a.y = 0f;
        worldPos.y = 0f;
        return Vector3.Distance(a, worldPos);
    }

    private bool IsInMeleeRange(Enemy enemy, float buffer)
    {
        if (enemy == null)
            return false;
        return HorizontalDistance(enemy.GetEngagePosition()) <= GlobalSettings.GetMeleeEngageDistance() + buffer;
    }

    private static float PathLength(NavMeshPath path)
    {
        float dist = 0f;
        for (int i = 0; i < path.corners.Length - 1; i++)
            dist += Vector3.Distance(path.corners[i], path.corners[i + 1]);
        return dist;
    }

    private static Vector3 PointOnPath(NavMeshPath path, float distance)
    {
        if (path.corners.Length < 2)
            return path.corners.Length > 0 ? path.corners[0] : Vector3.zero;

        float accumulated = 0f;
        for (int i = 0; i < path.corners.Length - 1; i++)
        {
            float seg = Vector3.Distance(path.corners[i], path.corners[i + 1]);
            if (accumulated + seg >= distance)
            {
                float t = (distance - accumulated) / seg;
                return Vector3.Lerp(path.corners[i], path.corners[i + 1], t);
            }

            accumulated += seg;
        }

        return path.corners[path.corners.Length - 1];
    }

    private static bool ShowPaths =>
        GlobalSettings.Instance == null || GlobalSettings.Instance.showPath;

    private void EnsurePathLines()
    {
        if (rollPathLine == null)
        {
            rollPathLine = CreateLine("RollPathLine", Color.yellow, 0.2f);
        }

        if (targetPathLine == null)
        {
            var purple = new Color(0.75f, 0.25f, 1f, 0.9f);
            targetPathLine = CreateLine("TargetPathLine", purple, 0.12f);
        }
    }

    private LineRenderer CreateLine(string name, Color color, float width)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform);
        go.layer = 0;
        var line = go.AddComponent<LineRenderer>();
        line.startWidth = width;
        line.endWidth = width;
        if (sharedPathLineMaterial == null)
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader != null)
                sharedPathLineMaterial = new Material(shader);
        }

        if (sharedPathLineMaterial != null)
            line.sharedMaterial = sharedPathLineMaterial;
        line.startColor = color;
        line.endColor = color;
        line.positionCount = 0;
        line.enabled = false;
        return line;
    }

    private void DrawRollPath(NavMeshPath path, float maxDistance)
    {
        if (!ShowPaths || rollPathLine == null || path.corners.Length < 2)
            return;

        pathPointScratch.Clear();
        pathPointScratch.Add(path.corners[0]);
        float accumulated = 0f;
        for (int i = 0; i < path.corners.Length - 1; i++)
        {
            Vector3 a = path.corners[i];
            Vector3 b = path.corners[i + 1];
            float seg = Vector3.Distance(a, b);
            if (accumulated + seg >= maxDistance)
            {
                pathPointScratch.Add(Vector3.Lerp(a, b, (maxDistance - accumulated) / seg));
                break;
            }

            accumulated += seg;
            pathPointScratch.Add(b);
        }

        ApplyLine(rollPathLine, pathPointScratch, 0.15f);
    }

    private void DrawTargetPath(NavMeshPath path)
    {
        if (!ShowPaths || targetPathLine == null || path.corners.Length < 2)
            return;

        pathPointScratch.Clear();
        for (int i = 0; i < path.corners.Length; i++)
            pathPointScratch.Add(path.corners[i]);

        ApplyLine(targetPathLine, pathPointScratch, 0.1f);
    }

    private static void ApplyLine(LineRenderer line, List<Vector3> points, float yOffset)
    {
        line.enabled = true;
        line.positionCount = points.Count;
        for (int i = 0; i < points.Count; i++)
            line.SetPosition(i, points[i] + Vector3.up * yOffset);
    }

    private void HideRollPathOnly()
    {
        if (rollPathLine == null)
            return;

        rollPathLine.enabled = false;
        rollPathLine.positionCount = 0;
    }

    private void HideTargetPath()
    {
        if (targetPathLine == null)
            return;

        targetPathLine.enabled = false;
        targetPathLine.positionCount = 0;
    }

    private void HideAllPaths()
    {
        HideRollPathOnly();
        HideTargetPath();
    }
}
