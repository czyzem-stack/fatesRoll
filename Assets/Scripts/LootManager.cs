using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Celebration loot: firework burst around the enemy, coins linger on the ground, then Steve collects them.
/// </summary>
[AddComponentMenu("FatesRoll/Loot Manager")]
public class LootManager : GameServiceBehaviour<LootManager>
{

    [Header("Coin drop count")]
    [Tooltip("Fewest coins spawned when an enemy dies.")]
    public int minCoins = 5;
    [Tooltip("Most coins spawned when an enemy dies (inclusive random range with Min Coins).")]
    public int maxCoins = 12;

    [Header("Celebration — firework burst")]
    [Tooltip("Seconds between each coin leaving the enemy (0 = all at once).")]
    public float burstSpawnStagger = 0.02f;
    [Tooltip("How long each coin flies outward before landing.")]
    public float popDuration = 0.7f;
    [Tooltip("Peak height of the outward arc.")]
    public float popArcHeight = 2.4f;
    [Tooltip("Min horizontal distance coins land from the enemy.")]
    public float fireworkRadiusMin = 1.4f;
    [Tooltip("Max horizontal distance coins land from the enemy.")]
    public float fireworkRadiusMax = 3.2f;
    [Tooltip("Height above enemy where the burst starts.")]
    public float spawnHeightOffset = 1.2f;

    [Header("Celebration — ground linger")]
    [Tooltip("Seconds coins sit on the ground after the burst before Steve collects them.")]
    [FormerlySerializedAs("collectDelay")]
    public float groundCelebrateDuration = 1.75f;
    [Tooltip("Spin speed while coins rest on the ground (deg/s).")]
    public float groundSpinSpeed = 90f;
    [Tooltip("Small bounce height while resting on the ground.")]
    public float groundBobHeight = 0.04f;
    [Tooltip("Speed of the idle bob while coins celebrate on the ground (higher = faster wiggle).")]
    public float groundBobSpeed = 6f;

    [Header("Pop & land")]
    [Tooltip("Visual prefab for dropped coins (e.g. Assets/Coins/Prefabs/Coins/coin_04).")]
    public GameObject coinPrefab;
    [Tooltip("Uniform scale multiplier applied to each spawned coin instance.")]
    public float coinScale = 1.25f;

    [Header("Collection — Steve pickup")]
    [Tooltip("How fast each coin flies toward Steve (m/s). Ramps up gently.")]
    [FormerlySerializedAs("collectFlySpeed")]
    public float stevePickupSpeed = 4f;
    [Tooltip("Seconds between each coin starting to move toward Steve.")]
    public float pickupStaggerDelay = 0.2f;
    [Tooltip("World-space offset from Steve's feet where coins fly to when collected (Y = chest height).")]
    public Vector3 heroCollectOffset = new Vector3(0f, 1.1f, 0f);

    [Header("Gold & UI")]
    [Tooltip("Gold added to Steve's balance for each coin that reaches him.")]
    public int goldPerCoin = 1;
    [Tooltip("Color of the floating '+X Gold' text above Steve after a batch is collected.")]
    public Color goldFloatingTextColor = new Color(1f, 0.82f, 0.2f, 1f);

    [Header("UI References")]
    [Tooltip("HUD text that shows Steve's current gold total. Use Auto-Assign UI if empty.")]
    public TextMeshProUGUI goldText;

    private int currentGold;
    private readonly List<DroppedCoin> activeCoins = new List<DroppedCoin>();
    private int pendingBatchCoins;
    private int pendingBatchGold;
    private bool collectPhaseActive;
    private HeroController cachedHero;
    private static readonly int GroundRayMask = ~0;

    public bool IsCollectPhaseActive => collectPhaseActive;

    private void Reset()
    {
#if UNITY_EDITOR
        if (coinPrefab == null)
            coinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Coins/Prefabs/Coins/coin_04.prefab");
#endif
    }

    protected override void Awake()
    {
        base.Awake();
        EnsureCoinPrefabAssigned();
    }

    private void EnsureCoinPrefabAssigned()
    {
        if (coinPrefab != null) return;
#if UNITY_EDITOR
        coinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Coins/Prefabs/Coins/coin_04.prefab");
#endif
        if (coinPrefab == null)
            Debug.LogWarning("LootManager: assign coin_04 prefab on LootManager in the scene.");
    }

    private void Start()
    {
        currentGold = GlobalSettings.GetStartingCoinBalance();
        cachedHero = GameServices.Hero;
        UpdateGoldUI();
    }

    public void OnEnemyDied(Enemy enemy)
    {
        if (enemy == null) return;
        EnsureCoinPrefabAssigned();
        if (coinPrefab == null)
        {
            Debug.LogWarning("LootManager: coinPrefab not assigned — no coin visuals.");
            return;
        }

        if (cachedHero == null)
            cachedHero = GameServices.Hero;

        Vector3 burstCenter = enemy.transform.position + Vector3.up * spawnHeightOffset;
        StartCoroutine(CelebrationLootRoutine(burstCenter));
    }

    private IEnumerator CelebrationLootRoutine(Vector3 burstCenter)
    {
        int bonus = RogueLiteManager.Instance != null ? RogueLiteManager.Instance.BonusCoinsPerEnemyKill : 0;
        int count = Random.Range(minCoins, maxCoins + 1) + bonus;
        pendingBatchCoins = count;
        pendingBatchGold = count * goldPerCoin;
        collectPhaseActive = false;

        for (int i = 0; i < count; i++)
        {
            SpawnFireworkCoin(burstCenter);
            if (i < count - 1 && burstSpawnStagger > 0f)
                yield return new WaitForSeconds(burstSpawnStagger);
        }

        // Let the full burst arc finish landing.
        yield return new WaitForSeconds(popDuration + 0.15f);

        // Coins rest on the ground — celebration beat before Steve collects.
        yield return new WaitForSeconds(groundCelebrateDuration);

        collectPhaseActive = true;
        var coinsToCollect = new List<DroppedCoin>(activeCoins);
        for (int i = 0; i < coinsToCollect.Count; i++)
        {
            if (coinsToCollect[i] != null)
                coinsToCollect[i].BeginCollect();
            if (i < coinsToCollect.Count - 1 && pickupStaggerDelay > 0f)
                yield return new WaitForSeconds(pickupStaggerDelay);
        }

        float pickupWindow = coinsToCollect.Count * (pickupStaggerDelay + 0.35f) + 5f;
        yield return new WaitForSeconds(pickupWindow);
        if (pendingBatchCoins > 0)
            ForceCompleteBatch();
    }

    private void SpawnFireworkCoin(Vector3 burstCenter)
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float dist = Random.Range(fireworkRadiusMin, fireworkRadiusMax);
        Vector3 outward = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
        Vector3 landXZ = burstCenter + outward;
        float groundY = SampleGroundY(landXZ);
        Vector3 landPos = new Vector3(landXZ.x, groundY + 0.08f, landXZ.z);

        float arc = popArcHeight * Mathf.Lerp(0.85f, 1.15f, dist / Mathf.Max(fireworkRadiusMax, 0.01f));

        GameObject go = Instantiate(coinPrefab, burstCenter, Random.rotation);
        go.name = "DroppedCoin";
        go.transform.localScale = Vector3.one * coinScale;

        StripPhysicsComponents(go);

        var coin = go.GetComponent<DroppedCoin>();
        if (coin == null) coin = go.AddComponent<DroppedCoin>();
        coin.Initialize(
            this,
            cachedHero,
            burstCenter,
            landPos,
            popDuration,
            arc,
            groundSpinSpeed,
            groundBobHeight,
            groundBobSpeed,
            stevePickupSpeed,
            heroCollectOffset);
    }

    private static void StripPhysicsComponents(GameObject go)
    {
        foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true))
            Destroy(rb);
        foreach (var col in go.GetComponentsInChildren<Collider>(true))
            Destroy(col);
    }

    private static float SampleGroundY(Vector3 near)
    {
        Vector3 rayStart = near + Vector3.up * 8f;
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 25f, GroundRayMask, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        if (Physics.Raycast(rayStart, Vector3.down, out hit, 25f))
            return hit.point.y;
        return near.y;
    }

    public void RegisterCoin(DroppedCoin coin)
    {
        if (coin != null && !activeCoins.Contains(coin))
            activeCoins.Add(coin);
    }

    public void NotifyCoinCollected(DroppedCoin coin)
    {
        activeCoins.Remove(coin);
        pendingBatchCoins--;
        if (pendingBatchCoins > 0) return;
        CompleteBatch();
    }

    private void ForceCompleteBatch()
    {
        if (pendingBatchCoins <= 0) return;

        for (int i = activeCoins.Count - 1; i >= 0; i--)
        {
            if (activeCoins[i] != null)
                Destroy(activeCoins[i].gameObject);
        }
        activeCoins.Clear();
        pendingBatchCoins = 0;
        CompleteBatch();
        GlobalSettings.LogGameplayWarning("LootManager: coin batch timed out before collection finished.");
    }

    private void CompleteBatch()
    {
        int amount = pendingBatchGold;
        pendingBatchGold = 0;
        collectPhaseActive = false;
        GrantBatchGold(amount);
    }

    private void GrantBatchGold(int amount)
    {
        if (amount <= 0) return;

        currentGold += amount;
        UpdateGoldUI();
        SpawnGoldFloatingText(amount);
        GlobalSettings.LogGameplay($"LootManager: +{amount} gold (total {currentGold})");
    }

    public void SpawnGoldFloatingText(int amount)
    {
        if (cachedHero == null)
            cachedHero = GameServices.Hero;
        if (cachedHero == null) return;

        GameObject go = new GameObject("FloatingText_Gold");
        go.transform.position = cachedHero.transform.position + Vector3.up * 2.4f;
        var ft = go.AddComponent<FloatingText>();
        ft.Setup($"+{amount} Gold", goldFloatingTextColor);
    }

    private void UpdateGoldUI()
    {
        if (goldText != null)
            goldText.text = currentGold.ToString("N0");
    }

    public void ResetGoldToStarting()
    {
        currentGold = GlobalSettings.GetStartingCoinBalance();
        UpdateGoldUI();
    }

    [ContextMenu("Auto-Assign UI")]
    public void AutoAssignUI()
    {
        string[] paths =
        {
            "MainUI_Canvas/HUD_Resources/HUD_Item_Gold/Gold/Text (TMP)",
            "MainUI_Canvas/HUD_Resources/HUD_Item_Gold/Text (TMP)",
            "MainUI_Canvas/HUD_Resources/HUD_Item_Coin/Coin/Text (TMP)",
            "MainUI_Canvas/HUD_Resources/HUD_Item_Coin/Text (TMP)",
        };

        foreach (string path in paths)
        {
            GameObject go = GameObject.Find(path);
            if (go == null) continue;
            goldText = go.GetComponent<TextMeshProUGUI>();
            if (goldText != null) break;
        }

        UpdateGoldUI();
    }
}
