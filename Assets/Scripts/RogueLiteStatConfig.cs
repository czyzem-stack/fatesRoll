using System;
using UnityEngine;

/// <summary>Per-stat tuning for <see cref="RogueLiteManager"/> level-up offers.</summary>
[Serializable]
public class RogueLiteStatConfig
{
    [Tooltip("Bonus per pick before scaler (e.g. +1 Strength, −1s energy regen, +1 coin per enemy).")]
    public float upgradeAmount = 1f;

    [Tooltip("Chance weight for this stat in the A/B offer pool. Example: 20 with all stats at 20 ≈ equal odds.")]
    [Min(0f)]
    public float offerChancePercent = 20f;

    [Tooltip("Scales repeat picks: amount × scaler^timesAlreadyChosen. 1 = same bonus every time; 1.1 → +1, +1.1, +1.21…")]
    [Min(0f)]
    public float upgradeScaler = 1f;

    public RogueLiteButtonColor buttonColor = RogueLiteButtonColor.Blue;

    public bool IsOffered => offerChancePercent > 0f;

    public float GetScaledAmount(int timesAlreadyChosen)
    {
        float scaler = upgradeScaler <= 0f ? 1f : upgradeScaler;
        return upgradeAmount * Mathf.Pow(scaler, timesAlreadyChosen);
    }
}
