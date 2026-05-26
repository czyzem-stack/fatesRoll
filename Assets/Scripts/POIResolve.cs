using UnityEngine;

public static class POIResolve
{
    public static void Resolve(GameObject target)
    {
        if (target == null) return;

        if (target.GetComponentInParent<SpawnNode>() != null)
        {
            SpawnManager.Instance?.ResolveEncounterFromEnemy(target);
            return;
        }

        if (POIManager.Instance != null)
            POIManager.Instance.ResolveVisitPOI(target);
        else
            Object.Destroy(target);
    }
}
