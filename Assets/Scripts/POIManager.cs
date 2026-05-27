using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/// <summary>Visit-order POIs only. When all are consumed, enables SpawnManager (SpawnNode markers).</summary>
/// <remarks>Inherits <see cref="GameServiceBehaviour{T}"/> — auto-registers in Awake via <see cref="GameServices"/>.</remarks>
[DefaultExecutionOrder(-50)]
public class POIManager : GameServiceBehaviour<POIManager>
{
    [SerializeField] private string gameplaySceneName = MainSceneGameplayGate.DefaultMainSceneName;

    private readonly List<POINode> activeVisitPOIs = new List<POINode>();
    private readonly List<POINode> allVisitPOIs = new List<POINode>();

    public bool HasInitialized { get; private set; }
    public int ActiveVisitCount => activeVisitPOIs.Count;
    public int VisitPoiCount => allVisitPOIs.Count;

    protected override void Start()
    {
        base.Start();
        TryRefreshForScene(SceneManager.GetActiveScene());
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryRefreshForScene(scene);
    }

    private void TryRefreshForScene(Scene scene)
    {
        if (!IsGameplayScene(scene))
            return;

        StartCoroutine(InitializeVisitPOIsNextFrame());
    }

    private bool IsGameplayScene(Scene scene) =>
        scene.IsValid() &&
        scene.name.Equals(gameplaySceneName, System.StringComparison.OrdinalIgnoreCase);

    /// <summary>Re-scan main scene POIs (bootstrap Awake runs before main exists).</summary>
    public void RefreshFromScene()
    {
        if (IsGameplayScene(SceneManager.GetActiveScene()))
            StartCoroutine(InitializeVisitPOIsNextFrame());
    }

    private IEnumerator InitializeVisitPOIsNextFrame()
    {
        yield return null;
        InitializeVisitPOIs();
    }

    private void InitializeVisitPOIs()
    {
        activeVisitPOIs.Clear();
        allVisitPOIs.Clear();
        HasInitialized = false;

        if (!GameServices.IsInitialized)
        {
            Debug.LogWarning("POIManager: GameServices not ready — deferring POI scan.");
            return;
        }

        var found = Object.FindObjectsByType<POINode>(FindObjectsInactive.Include);
        foreach (var poi in found)
        {
            if (poi != null && IsGameplayScene(poi.gameObject.scene))
                allVisitPOIs.Add(poi);
        }

        foreach (var poi in allVisitPOIs)
            ActivateVisitPOI(poi);

        HasInitialized = true;
        GlobalSettings.LogGameplay($"POIManager: initialized {allVisitPOIs.Count} visit POI(s) in {gameplaySceneName}.");

        if (allVisitPOIs.Count == 0)
            Debug.LogWarning($"POIManager: no POINode objects found in {gameplaySceneName}.");

        if (allVisitPOIs.Count > 0 && !HasRemainingVisitPOI())
            TryEnableRandomVisitTargeting();
    }

    public void RegisterPOI(POINode poi)
    {
        if (poi == null)
            return;

        if (!allVisitPOIs.Contains(poi))
            allVisitPOIs.Add(poi);

        if (activeVisitPOIs.Contains(poi))
            return;

        activeVisitPOIs.Add(poi);
    }

    public void UnregisterPOI(POINode poi)
    {
        activeVisitPOIs.Remove(poi);
        allVisitPOIs.Remove(poi);
    }

    public void ResolveVisitPOI(GameObject poiObject)
    {
        var hero = GameServices.Hero;
        POINode node = poiObject != null ? poiObject.GetComponentInParent<POINode>() : null;

        if (hero != null && hero.currentEnemy != null)
        {
            var go = hero.currentEnemy;
            if (go == poiObject || (node != null && go.transform.IsChildOf(node.transform)))
                hero.ExitCombat();
        }

        if (node == null)
        {
            if (poiObject != null)
                Destroy(poiObject);
            return;
        }

        UnregisterPOI(node);
        Destroy(node.gameObject);
        TryEnableRandomVisitTargeting();
    }

    private void ActivateVisitPOI(POINode poi)
    {
        if (poi == null)
            return;

        poi.gameObject.SetActive(true);

        if (poi.IsTreasureChest && poi.currentVisual == null)
            POIVisualBuilder.BuildVisuals(poi);

        if (!activeVisitPOIs.Contains(poi))
            RegisterPOI(poi);

        poi.InitializeEnemy();
    }

    public bool HasRemainingVisitPOI(POINode excludeDefeated = null)
    {
        foreach (var poi in allVisitPOIs)
        {
            if (poi == null || poi == excludeDefeated)
                continue;
            return true;
        }

        return false;
    }

    public bool HasRemainingFtuePOI() => HasRemainingVisitPOI();

    public void TryEnableRandomVisitTargeting()
    {
        if (HasRemainingVisitPOI())
            return;

        if (GameServices.TryGet(out SpawnManager spawnManager))
            spawnManager.EnableRandomVisitTargeting();
    }

    public GameObject GetNextPOITarget(int visitOrder)
    {
        if (HasRemainingVisitPOI())
        {
            GameObject visit = GetVisitPOIByOrder(visitOrder);
            if (visit != null)
                return visit;
        }

        return GameServices.TryGet(out SpawnManager spawnManager)
            ? spawnManager.GetRandomSpawnTarget()
            : null;
    }

    public GameObject GetNearestPOI(Vector3 position)
    {
        POINode nearest = null;
        float minDist = float.MaxValue;

        foreach (var poi in activeVisitPOIs)
        {
            if (poi == null || !poi.gameObject.activeInHierarchy)
                continue;

            float dist = Vector3.Distance(position, poi.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = poi;
            }
        }

        if (nearest != null)
            return nearest.gameObject;

        return GameServices.TryGet(out SpawnManager spawnManager)
            ? spawnManager.GetNearestSpawn(position)
            : null;
    }

    public GameObject GetVisitPOIByOrder(int order)
    {
        POINode exact = null;
        POINode nextHigher = null;
        int nextHigherOrder = int.MaxValue;
        POINode lowestRemaining = null;
        int lowestOrder = int.MaxValue;

        foreach (var poi in allVisitPOIs)
        {
            if (poi == null)
                continue;

            if (poi.order < lowestOrder)
            {
                lowestOrder = poi.order;
                lowestRemaining = poi;
            }

            if (poi.order == order)
                exact = poi;
            else if (poi.order > order && poi.order < nextHigherOrder)
            {
                nextHigherOrder = poi.order;
                nextHigher = poi;
            }
        }

        POINode pick = exact ?? nextHigher ?? lowestRemaining;
        if (pick == null)
            return null;

        if (!pick.gameObject.activeInHierarchy)
            ActivateVisitPOI(pick);

        return pick.gameObject;
    }

    public GameObject GetPOIByOrder(int order) => GetVisitPOIByOrder(order);
    public GameObject GetSequentialPOIByOrder(int order) => GetVisitPOIByOrder(order);

    public void RefreshRemainingVisitEnemies()
    {
        foreach (var poi in allVisitPOIs)
        {
            if (poi == null || poi.IsTreasureChest)
                continue;

            var enemy = poi.GetComponent<Enemy>();
            if (enemy == null)
                continue;

            enemy.ReviveForRunReset(poi);
        }
    }

    public void ResolveSequentialPOI(GameObject go) => ResolveVisitPOI(go);
}
