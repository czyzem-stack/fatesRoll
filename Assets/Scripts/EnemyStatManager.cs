using UnityEngine;

/// <summary>
/// Scales enemy primary stats from Steve's current build. Used for spawn slots and optional FTUE tuning.
/// </summary>
/// <remarks>Inherits <see cref="GameServiceBehaviour{T}"/> — auto-registers in Awake via <see cref="GameServices"/>.</remarks>
public class EnemyStatManager : GameServiceBehaviour<EnemyStatManager>
{

    [Header("Difficulty")]
    [Tooltip("100 = enemy primaries match Steve's effective stats. 0–200 for easier/harder infinite scaling.")]
    [Range(0f, 200f)]
    [SerializeField] private float difficultyPercent = 100f;

    [Tooltip("Added to difficulty % after each random-pool kill (infinite ramp).")]
    [SerializeField] private float difficultyPerRandomKill = 0f;

    [Header("FTUE (optional)")]
    [Tooltip("When no EnemyData on a sequential POI, scale Steve by this % times the step multiplier below.")]
    [SerializeField] private float ftueBasePercent = 50f;

    [SerializeField] private float ftuePercentPerVisitOrder = 8f;

    private float randomKillBonus;

    public float DifficultyPercent => difficultyPercent + randomKillBonus;

    public void NotifyRandomPoolKill()
    {
        randomKillBonus += difficultyPerRandomKill;
    }

    public void ResetRunBonuses()
    {
        randomKillBonus = 0f;
    }

    public void ApplyScaledStats(Enemy enemy)
    {
        if (enemy == null) return;

        if (!TryGetSteveStats(out float str, out float agi, out float vit, out float luck))
        {
            enemy.Initialize();
            return;
        }

        float scale = DifficultyPercent / 100f;
        enemy.strength = str * scale;
        enemy.agility = agi * scale;
        enemy.vitality = vit * scale;
        enemy.luck = luck * scale;
        enemy.Initialize();
    }

    /// <summary>Sequential POI without authored EnemyData.</summary>
    public void ApplyFtueStepStats(Enemy enemy, int visitOrder)
    {
        if (enemy == null) return;

        if (!TryGetSteveStats(out float str, out float agi, out float vit, out float luck))
        {
            enemy.Initialize();
            return;
        }

        float stepScale = (ftueBasePercent + visitOrder * ftuePercentPerVisitOrder) / 100f;
        enemy.strength = str * stepScale;
        enemy.agility = agi * stepScale;
        enemy.vitality = vit * stepScale;
        enemy.luck = luck * stepScale;
        enemy.Initialize();
    }

    private static bool TryGetSteveStats(out float str, out float agi, out float vit, out float luck)
    {
        str = agi = vit = luck = 0f;
        var hero = GameServices.Hero;
        if (hero == null) return false;

        var stats = hero.GetComponent<PlayerStats>();
        if (stats == null) return false;

        stats.GetEffectivePrimaries(out str, out agi, out vit, out luck);
        return true;
    }
}
