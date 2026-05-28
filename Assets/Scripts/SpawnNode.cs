using UnityEngine;

public enum SpawnNodeKind
{
    Monster,
    TreasureChest
}

public enum MonsterType
{
    None,
    Orc,
    Skeleton,
    Slime,
    Bat,
    Dragon,
    EvilMage,
    Golem,
    MonsterPlant,
    Spider,
    TurtleShell
}

/// <summary>Marker for where SpawnManager can spawn a random enemy or treasure chest (not a visit-order POI).</summary>
public class SpawnNode : MonoBehaviour
{
    [Tooltip("Monster = rotating combat. TreasureChest = equipment loot on arrival.")]
    public SpawnNodeKind spawnKind = SpawnNodeKind.Monster;

    [Header("Monster Configuration")]
    public MonsterType monsterType = MonsterType.Orc;
    
    [Header("Base Stats (Initial Spawn)")]
    public float baseStrength = 8f;
    public float baseAgility = 8f;
    public float baseVitality = 8f;
    public float baseLuck = 5f;

    [Header("Reward")]
    public int minGoldReward = 5;
    public int maxGoldReward = 12;

    [HideInInspector] public bool hasSpawnedType;
    [HideInInspector] public POIType lastSpawnedType;
    [HideInInspector] public GameObject activeEncounter;
    [HideInInspector] public int killCount = 0;

    public bool IsOccupied => activeEncounter != null;
    public bool IsRespawn => killCount > 0;

    public POIType? GetLastSpawnedType() => hasSpawnedType ? lastSpawnedType : (POIType?)null;

    public void RecordSpawnedType(POIType type)
    {
        lastSpawnedType = type;
        hasSpawnedType = true;
    }

    public void IncrementKillCount()
    {
        killCount++;
    }

    public Enemy GetActiveEnemy()
    {
        if (activeEncounter == null) return null;
        return activeEncounter.GetComponent<Enemy>();
    }

    /// <summary>Maps MonsterType to POIType for visual compatibility with existing systems.</summary>
    public POIType GetPOIType()
    {
        if (spawnKind == SpawnNodeKind.TreasureChest) return POIType.TreasureChest;
        
        // Match enum names for easy mapping
        if (System.Enum.TryParse(monsterType.ToString(), out POIType poiType))
            return poiType;
            
        return POIType.Orc;
    }

    public void ClearEncounter()
    {
        if (activeEncounter != null)
        {
            if (Application.isPlaying)
                Destroy(activeEncounter);
            else
                DestroyImmediate(activeEncounter);
            activeEncounter = null;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (spawnKind == SpawnNodeKind.TreasureChest)
        {
            Gizmos.color = IsOccupied ? new Color(1f, 0.75f, 0.1f, 0.95f) : new Color(1f, 0.85f, 0.2f, 0.85f);
        }
        else
            Gizmos.color = IsOccupied ? new Color(1f, 0.35f, 0.1f, 0.9f) : new Color(0.2f, 0.85f, 1f, 0.75f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, 0.65f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 1.5f);
    }
#endif
}
