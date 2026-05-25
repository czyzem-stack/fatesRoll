using UnityEngine;

public enum POIType
{
    Orc,
    Skeleton,
    Slime
}

public class POINode : MonoBehaviour
{
    public POIType type = POIType.Orc;
    
    [HideInInspector]
    public GameObject currentVisual;

    void Awake()
    {
        gameObject.tag = "POI";
    }

    void Start()
    {
        if (POIManager.Instance != null)
        {
            POIManager.Instance.RegisterPOI(this);
        }

        // Visuals should already be present from Editor or Spawn
        InitializeEnemy();
    }

    public void InitializeEnemy()
    {
        Enemy enemy = GetComponent<Enemy>();
        if (enemy != null)
        {
            enemy.Initialize();
        }
    }

    void OnDestroy()
    {
        if (POIManager.Instance != null)
        {
            POIManager.Instance.UnregisterPOI(this);
        }
    }
}