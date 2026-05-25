using UnityEngine;
using System.Collections.Generic;

public class POIManager : MonoBehaviour
{
    private static POIManager _instance;
    public static POIManager Instance
    {
        get
        {
            if (_instance == null) _instance = Object.FindAnyObjectByType<POIManager>();
            return _instance;
        }
    }

    private List<POINode> activePOIs = new List<POINode>();

    public void RegisterPOI(POINode poi)
    {
        if (!activePOIs.Contains(poi)) activePOIs.Add(poi);
    }

    public void UnregisterPOI(POINode poi)
    {
        activePOIs.Remove(poi);
    }

    public void ResolvePOI(GameObject poiObject)
    {
        POINode node = poiObject.GetComponentInParent<POINode>();
        if (node != null)
        {
            UnregisterPOI(node);
            Destroy(node.gameObject);
        }
        else
        {
            Destroy(poiObject);
        }
    }

    public GameObject GetNearestPOI(Vector3 position)
    {
        POINode nearest = null;
        float minDist = float.MaxValue;

        foreach (var poi in activePOIs)
        {
            if (poi == null) continue;
            float dist = Vector3.Distance(position, poi.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = poi;
            }
        }

        return nearest != null ? nearest.gameObject : null;
    }

    public GameObject GetRandomPOI()
    {
        if (activePOIs.Count == 0) return null;
        int index = Random.Range(0, activePOIs.Count);
        return activePOIs[index] != null ? activePOIs[index].gameObject : null;
    }
}