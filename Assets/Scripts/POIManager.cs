using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public enum POIType
{
    Orc,
    Skeleton,
    Slime,
    Cyclops,
    Beholder,
    BishopKnight
}

[System.Serializable]
public struct POIDefinition
{
    public POIType type;
    public GameObject prefab;
}

public class POIManager : MonoBehaviour
{
    [Header("Global Registry")]
    public List<POIDefinition> poiPrefabs = new List<POIDefinition>();

    private static POIManager _instance;
    public static POIManager Instance
    {
        get
        {
            if (_instance == null) _instance = Object.FindAnyObjectByType<POIManager>();
            return _instance;
        }
    }

    private void Awake()
    {
        _instance = this;
    }

    public GameObject GetPrefabForType(POIType type)
    {
        foreach (var def in poiPrefabs)
        {
            if (def.type == type) return def.prefab;
        }
        return null;
    }

    [ContextMenu("Resolve Current POI")]
    public void ResolveActivePOI()
    {
        var poi = GameObject.FindWithTag("POI");
        if (poi != null)
        {
            ResolvePOI(poi);
        }
    }

    public void ResolvePOI(GameObject poiObject)
    {
        if (poiObject != null)
        {
            // If it's a child, get the root node
            POINode node = poiObject.GetComponentInParent<POINode>();
            if (node != null)
            {
                Destroy(node.gameObject);
            }
            else
            {
                Destroy(poiObject);
            }
        }
    }

    }
