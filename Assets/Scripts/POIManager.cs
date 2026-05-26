using UnityEngine;
using System.Collections.Generic;

/// <summary>Visit-order POIs only. When all are consumed, enables SpawnManager (SpawnNode markers).</summary>
[DefaultExecutionOrder(-50)]
public class POIManager : GameServiceBehaviour<POIManager>
{

    private readonly List<POINode> activeVisitPOIs = new List<POINode>();
    private readonly List<POINode> allVisitPOIs = new List<POINode>();

    public bool HasInitialized { get; private set; }
    public int ActiveVisitCount => activeVisitPOIs.Count;

    private void Start()
    {
        InitializeVisitPOIs();
    }

    private void InitializeVisitPOIs()
    {
        activeVisitPOIs.Clear();
        allVisitPOIs.Clear();

        var found = Object.FindObjectsByType<POINode>(FindObjectsInactive.Include);
        foreach (var poi in found)
        {
            if (poi != null)
                allVisitPOIs.Add(poi);
        }

        foreach (var poi in allVisitPOIs)
            ActivateVisitPOI(poi);

        HasInitialized = true;

        if (!HasRemainingVisitPOI())
            TryEnableRandomVisitTargeting();
    }

    public void RegisterPOI(POINode poi)
    {
        if (poi == null || activeVisitPOIs.Contains(poi)) return;
        activeVisitPOIs.Add(poi);
    }

    public void UnregisterPOI(POINode poi)
    {
        activeVisitPOIs.Remove(poi);
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
            if (poiObject != null) Destroy(poiObject);
            return;
        }

        UnregisterPOI(node);
        allVisitPOIs.Remove(node);
        Destroy(node.gameObject);
        TryEnableRandomVisitTargeting();
    }

    private void ActivateVisitPOI(POINode poi)
    {
        if (poi == null) return;

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
            if (poi == null || poi == excludeDefeated) continue;
            return true;
        }
        return false;
    }

    public bool HasRemainingFtuePOI() => HasRemainingVisitPOI();

    public void TryEnableRandomVisitTargeting()
    {
        if (!HasRemainingVisitPOI() && SpawnManager.Instance != null)
            SpawnManager.Instance.EnableRandomVisitTargeting();
    }

    public GameObject GetNextPOITarget(int visitOrder)
    {
        if (HasRemainingVisitPOI())
        {
            GameObject visit = GetVisitPOIByOrder(visitOrder);
            if (visit != null)
                return visit;
        }

        return SpawnManager.Instance != null
            ? SpawnManager.Instance.GetRandomSpawnTarget()
            : null;
    }

    public GameObject GetNearestPOI(Vector3 position)
    {
        POINode nearest = null;
        float minDist = float.MaxValue;

        foreach (var poi in activeVisitPOIs)
        {
            if (poi == null || !poi.gameObject.activeInHierarchy) continue;
            float dist = Vector3.Distance(position, poi.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = poi;
            }
        }

        if (nearest != null)
            return nearest.gameObject;

        return SpawnManager.Instance != null
            ? SpawnManager.Instance.GetNearestSpawn(position)
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
            if (poi == null) continue;

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
        if (pick == null) return null;

        if (!pick.gameObject.activeInHierarchy)
            ActivateVisitPOI(pick);

        return pick.gameObject;
    }

    public GameObject GetPOIByOrder(int order) => GetVisitPOIByOrder(order);
    public GameObject GetSequentialPOIByOrder(int order) => GetVisitPOIByOrder(order);

    /// <summary>Revive and re-scale visit POI enemies still in the scene (spawn pool handled separately).</summary>
    public void RefreshRemainingVisitEnemies()
    {
        foreach (var poi in allVisitPOIs)
        {
            if (poi == null || poi.IsTreasureChest) continue;

            var enemy = poi.GetComponent<Enemy>();
            if (enemy == null) continue;

            enemy.ReviveForRunReset(poi);
        }
    }

    // Back-compat for any callers
    public void ResolveSequentialPOI(GameObject go) => ResolveVisitPOI(go);
}
