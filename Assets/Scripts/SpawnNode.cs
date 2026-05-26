using UnityEngine;

public enum SpawnNodeKind
{
    Monster,
    TreasureChest
}

/// <summary>Marker for where SpawnManager can spawn a random enemy or treasure chest (not a visit-order POI).</summary>
public class SpawnNode : MonoBehaviour
{
    [Tooltip("Monster = rotating combat. TreasureChest = equipment loot on arrival.")]
    public SpawnNodeKind spawnKind = SpawnNodeKind.Monster;

    [HideInInspector] public bool hasSpawnedType;
    [HideInInspector] public POIType lastSpawnedType;
    [HideInInspector] public GameObject activeEncounter;

    public bool IsOccupied => activeEncounter != null;

    public POIType? GetLastSpawnedType() => hasSpawnedType ? lastSpawnedType : null;

    public void RecordSpawnedType(POIType type)
    {
        lastSpawnedType = type;
        hasSpawnedType = true;
    }

    public Enemy GetActiveEnemy()
    {
        if (activeEncounter == null) return null;
        return activeEncounter.GetComponent<Enemy>();
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
