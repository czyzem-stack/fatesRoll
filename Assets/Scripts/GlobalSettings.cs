using UnityEngine;

public class GlobalSettings : MonoBehaviour
{
    private static GlobalSettings _instance;
    public static GlobalSettings Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Object.FindAnyObjectByType<GlobalSettings>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("GlobalSettings");
                    _instance = go.AddComponent<GlobalSettings>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    [Header("Movement Settings")]
    [Tooltip("How many steps Steve takes per dice number (e.g. if 1:1, roll of 5 = 5 steps)")]
    public float stepsPerDiceValue = 1.0f;
    
    [Tooltip("Distance in meters for a single 'step'")]
    public float metersPerStep = 3.0f;

    [Header("Energy Settings")]
    public int energyDepletionPerRoll = 3;
    public int startingEnergy = 60;
    public int maxEnergy = 60;

    [Header("XP & Leveling Settings")]
    public float baseXPForLevel1 = 50f;
    public float xpExponentialMultiplier = 1.2f;

    [Header("QA Settings")]
    public bool showPath = true;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
