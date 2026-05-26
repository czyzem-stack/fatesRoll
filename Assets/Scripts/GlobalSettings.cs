using UnityEngine;

/// <summary>
/// Global tuning for movement, energy, combat spacing/timing, and XP.
/// Hero combat stats: <see cref="PlayerStats"/> on Steve.
/// Enemy combat stats: <see cref="Enemy"/> on each monster.
/// </summary>
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

    [Header("Movement")]
    [Tooltip("How many steps Steve takes per dice number (e.g. if 1:1, roll of 5 = 5 steps)")]
    public float stepsPerDiceValue = 1.0f;

    [Tooltip("Distance in meters for a single 'step'")]
    public float metersPerStep = 3.0f;

    [Tooltip("Steve's walk speed when moving along the dice path")]
    public float heroTravelSpeed = 6f;

    [Header("Energy")]
    public int energyDepletionPerRoll = 3;
    public int startingEnergy = 60;
    public int maxEnergy = 60;
    public float energyRegenInterval = 15f;
    public int energyRegenAmount = 1;
    public float energyDisplayTotalDuration = 15f;
    public float energyDisplayNextDuration = 3f;

    [Header("Loot")]
    [Tooltip("Steve's coin/gold balance when a run starts.")]
    public int startingCoinBalance = 100;

    public static int GetStartingCoinBalance()
    {
        var s = Instance;
        return s != null ? s.startingCoinBalance : 100;
    }

    [Header("Combat — spacing")]
    [Tooltip("Horizontal distance from enemy center where Steve can start melee and paths aim to stop.")]
    public float meleeEngageDistance = 2.5f;

    public static float GetMeleeEngageDistance()
    {
        var s = Instance;
        return s != null ? s.meleeEngageDistance : 2.5f;
    }

    [Header("Combat — timing (seconds)")]
    public float combatDiceReadDelay = 0.2f;
    public float travelDiceReadDelay = 0.85f;
    public float combatFaceDelay = 0.06f;
    public float combatHeroHitDelay = 0.22f;
    public float combatReactionDelay = 0.18f;
    public float enemyAttackWindUp = 0.14f;
    public float enemyAttackHitDelay = 0.26f;

    [Header("XP & leveling")]
    public float baseXPForLevel1 = 50f;
    public float xpExponentialMultiplier = 1.2f;

    [Header("Debug")]
    [Tooltip("Log combat events to the Console (filter: Combat).")]
    public bool combatLogEnabled = true;

    [Tooltip("Log dice, movement, energy, XP, and die physics to the Console.")]
    public bool verboseGameplayLogs = false;

    [Tooltip("Draw Steve's dice path and POI path lines.")]
    public bool showPath = true;

    /// <summary>Gameplay/system logs gated by <see cref="verboseGameplayLogs"/>.</summary>
    public static void LogGameplay(string message)
    {
        var s = Instance;
        if (s != null && s.verboseGameplayLogs)
            Debug.Log(message);
    }

    public static void LogGameplayWarning(string message)
    {
        var s = Instance;
        if (s != null && s.verboseGameplayLogs)
            Debug.LogWarning(message);
    }

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
