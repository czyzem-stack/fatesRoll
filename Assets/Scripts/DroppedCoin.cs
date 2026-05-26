using UnityEngine;

/// <summary>
/// Firework pop outward, celebrate on the ground, then magnet to Steve when LootManager allows.
/// </summary>
public class DroppedCoin : MonoBehaviour
{
    private enum Phase { Pop, Celebrate, Collect }

    private LootManager lootManager;
    private HeroController hero;
    private Phase phase = Phase.Pop;

    private Vector3 popStart;
    private Vector3 popEnd;
    private float popDuration;
    private float popArcHeight;
    private float popTimer;
    private float celebrateSpinSpeed;
    private float celebrateBobHeight;
    private float celebrateBobSpeed;
    private float celebrateBaseY;
    private float collectSpeed;
    private Vector3 collectTargetOffset;
    private float collectRamp;
    private bool registered;

    public void Initialize(
        LootManager manager,
        HeroController targetHero,
        Vector3 spawnPosition,
        Vector3 landPosition,
        float popSeconds,
        float arcHeight,
        float spinOnGround,
        float bobHeight,
        float bobSpeed,
        float magnetSpeed,
        Vector3 heroOffset)
    {
        lootManager = manager;
        hero = targetHero;
        popStart = spawnPosition;
        popEnd = landPosition;
        popDuration = Mathf.Max(0.25f, popSeconds);
        popArcHeight = arcHeight;
        celebrateSpinSpeed = spinOnGround;
        celebrateBobHeight = bobHeight;
        celebrateBobSpeed = bobSpeed;
        celebrateBaseY = landPosition.y;
        collectSpeed = magnetSpeed;
        collectTargetOffset = heroOffset;
        popTimer = 0f;
        collectRamp = 0f;
        phase = Phase.Pop;

        transform.position = popStart;
        EnsureRenderersEnabled();

        if (lootManager != null && !registered)
        {
            lootManager.RegisterCoin(this);
            registered = true;
        }
    }

    private void EnsureRenderersEnabled()
    {
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            r.enabled = true;
    }

    private void Update()
    {
        switch (phase)
        {
            case Phase.Pop:
                UpdatePop();
                break;
            case Phase.Celebrate:
                UpdateCelebrate();
                break;
            case Phase.Collect:
                UpdateMagnet();
                break;
        }
    }

    private void UpdatePop()
    {
        popTimer += Time.deltaTime;
        float t = Mathf.Clamp01(popTimer / popDuration);
        // Ease-out horizontal — bursts outward, eases onto the ground.
        float eased = 1f - (1f - t) * (1f - t);
        Vector3 pos = Vector3.Lerp(popStart, popEnd, eased);
        pos.y += popArcHeight * 4f * t * (1f - t);
        transform.position = pos;
        transform.Rotate(Vector3.up, 720f * Time.deltaTime, Space.World);

        if (t >= 1f)
        {
            transform.position = popEnd;
            celebrateBaseY = popEnd.y;
            phase = Phase.Celebrate;
        }
    }

    private void UpdateCelebrate()
    {
        float bob = Mathf.Sin(Time.time * celebrateBobSpeed) * celebrateBobHeight;
        Vector3 pos = transform.position;
        pos.y = celebrateBaseY + bob;
        transform.position = pos;
        transform.Rotate(Vector3.up, celebrateSpinSpeed * Time.deltaTime, Space.World);

        if (lootManager != null && lootManager.IsCollectPhaseActive)
            BeginCollect();
    }

    public void BeginCollect()
    {
        if (phase == Phase.Collect) return;
        phase = Phase.Collect;
        collectRamp = 0f;
        if (hero == null)
            hero = Object.FindAnyObjectByType<HeroController>();
    }

    private void UpdateMagnet()
    {
        if (hero == null)
            hero = Object.FindAnyObjectByType<HeroController>();

        if (hero == null)
        {
            if (lootManager != null && lootManager.IsCollectPhaseActive)
                return;
            Finish();
            return;
        }

        collectRamp = Mathf.Min(1f, collectRamp + Time.deltaTime * 1.8f);
        float speed = collectSpeed * collectRamp * collectRamp;

        Vector3 target = hero.transform.position + collectTargetOffset;
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
        transform.Rotate(Vector3.up, 480f * Time.deltaTime, Space.World);

        if ((transform.position - target).sqrMagnitude < 0.1f)
            Finish();
    }

    private void Finish()
    {
        if (lootManager != null)
            lootManager.NotifyCoinCollected(this);
        Destroy(gameObject);
    }
}
