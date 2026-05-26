using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MonsterPrefabCatalog", menuName = "FatesRoll/Monster Prefab Catalog")]
public class MonsterPrefabCatalog : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        public POIType type;
        public GameObject prefab;
        [Tooltip("Locomotion + combat controller (Speed, Attack). Filled by Build Monster Prefab Catalog.")]
        public RuntimeAnimatorController gameplayAnimator;
    }

    public Entry[] entries;

    [Tooltip("Used when a monster pack controller lacks Speed/Attack (e.g. Bat, Dragon).")]
    public RuntimeAnimatorController gameplayFallback;

    public GameObject GetPrefab(POIType type)
    {
        if (entries == null) return null;
        foreach (var e in entries)
        {
            if (e.type == type && e.prefab != null)
                return e.prefab;
        }
        return null;
    }

    public RuntimeAnimatorController GetGameplayAnimator(POIType type)
    {
        if (entries != null)
        {
            foreach (var e in entries)
            {
                if (e.type == type && e.gameplayAnimator != null)
                    return e.gameplayAnimator;
            }
        }

        return gameplayFallback;
    }

    public IReadOnlyList<POIType> GetTypesWithPrefabs()
    {
        var list = new List<POIType>();
        if (entries == null) return list;
        foreach (var e in entries)
        {
            if (e.prefab != null && e.type != POIType.TreasureChest)
                list.Add(e.type);
        }
        return list;
    }
}
