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

    [Tooltip("Steve's walk speed when moving along the dice path")]
    public float heroTravelSpeed = 6f;

    [Header("Energy Settings")]
public int energyDepletionPerRoll = 3;
    public int startingEnergy = 60;
    public int maxEnergy = 60;
    public float energyRegenInterval = 15f;
    public int energyRegenAmount = 1;
    public float energyDisplayTotalDuration = 15f;
    public float energyDisplayNextDuration = 3f;

    [Header("Combat Settings")]
    public int heroMaxHP = 20;
    public int orcStartHP = 10;
    public int combatDamageMultiplier = 2;
    public float leftoverStepDamageMultiplier = 50.0f;

    [Header("Combat Timing (seconds)")]
    public float combatDiceReadDelay = 0.2f;
    public float travelDiceReadDelay = 0.85f;
    public float combatFaceDelay = 0.06f;
    public float combatHeroHitDelay = 0.22f;
    public float combatReactionDelay = 0.18f;
    public float enemyAttackWindUp = 0.14f;
    public float enemyAttackHitDelay = 0.26f;

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
