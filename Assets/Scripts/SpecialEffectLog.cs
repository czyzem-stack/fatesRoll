using UnityEngine;

/// <summary>Color-coded console output for enemy specials and Steve residual effects. Filter Console with: Special</summary>
public static class SpecialEffectLog
{
    private const string Tag = "[Special]";

    private static bool IsEnabled
    {
        get
        {
            var settings = GlobalSettings.Instance;
            return settings == null || settings.specialEffectLogEnabled;
        }
    }

    public static string ColorFor(POIType type) => type switch
    {
        POIType.Orc => "#FFAA00",
        POIType.Skeleton => "#B0B0B0",
        POIType.Slime => "#55FF88",
        POIType.Bat => "#CC66FF",
        POIType.Dragon => "#FF6600",
        POIType.EvilMage => "#AA44FF",
        POIType.Golem => "#AA7744",
        POIType.MonsterPlant => "#66DD44",
        POIType.Spider => "#EEEEEE",
        POIType.TurtleShell => "#4488FF",
        _ => "#FFFFFF"
    };

    static string ColorForResidual(string effectName)
    {
        if (string.IsNullOrEmpty(effectName))
            return "#FF8888";

        string key = effectName.ToLowerInvariant();
        if (key.Contains("burn"))
            return "#FF8800";
        if (key.Contains("poison"))
            return "#44EE66";
        if (key.Contains("curse"))
            return "#BB66FF";
        if (key.Contains("fear"))
            return "#DD66FF";
        if (key.Contains("knock"))
            return "#AA7744";
        return "#FFAAAA";
    }

    static void Write(string message)
    {
        if (!IsEnabled || string.IsNullOrEmpty(message))
            return;

        Debug.Log($"{Tag} {message}");
    }

    static string EnemyLabel(POIType type, string enemyName) =>
        $"<color={ColorFor(type)}>{enemyName}</color>";

    /// <summary>Enemy activates a special ability (self-buff, flee, etc.).</summary>
    public static void EnemyTriggered(POIType type, string enemyName, string ability, string detail = null)
    {
        string extra = string.IsNullOrWhiteSpace(detail) ? "" : $" | {detail}";
        Write($"{EnemyLabel(type, enemyName)} <b>{ability}</b>{extra}");
    }

    /// <summary>Enemy special applies a debuff or effect on Steve.</summary>
    public static void AppliedToSteve(POIType type, string enemyName, string effect, string detail = null)
    {
        string c = ColorForResidual(effect);
        string extra = string.IsNullOrWhiteSpace(detail) ? "" : $" | {detail}";
        Write($"{EnemyLabel(type, enemyName)} → <color={c}>Steve: {effect}</color>{extra}");
    }

    /// <summary>Immediate damage from a special (opening hit on apply).</summary>
    public static void SteveImmediateDamage(POIType type, string enemyName, string effect, int damage, float hpAfter)
    {
        string c = ColorForResidual(effect);
        Write(
            $"{EnemyLabel(type, enemyName)} → <color={c}>{effect} hit</color> | -{damage} HP | Steve {hpAfter:F0} HP left");
    }

    /// <summary>Steve residual tick at start of dice roll (burn, poison).</summary>
    public static void SteveResidualTick(string effect, int damage, float hpAfter, int turnsRemainingAfterTick)
    {
        string c = ColorForResidual(effect);
        string turns = turnsRemainingAfterTick > 0
            ? $"{turnsRemainingAfterTick} turn(s) left"
            : "ended";

        if (damage <= 0)
        {
            Write($"<color={c}>Steve {effect}</color> | {hpAfter:F0} HP | {turns}");
            return;
        }

        Write(
            $"<color={c}>Steve {effect} tick</color> | -{damage} HP | {hpAfter:F0} HP left | {turns}");
    }

    public static void SteveEffectEnded(string effect)
    {
        Write($"<color={ColorForResidual(effect)}>Steve {effect}</color> wore off");
    }

    public static void EnemyEffectEnded(POIType type, string enemyName, string effect)
    {
        Write($"{EnemyLabel(type, enemyName)} {effect} wore off");
    }

    public static void EnemyRegenTick(string enemyName, float amount, float hpAfter)
    {
        Write($"<color={ColorFor(POIType.Slime)}>{enemyName}</color> REGEN | +{amount:F0} HP | {hpAfter:F0} HP");
    }

    public static void EnemyBlocked(POIType type, string enemyName, float roll, float blockChance)
    {
        Write(
            $"{EnemyLabel(type, enemyName)} <b>BLOCK</b> | roll {roll:F1} under {blockChance:F0}% block chance");
    }

    public static void EnemyDodged(string enemyName, float roll, float dodgeChance)
    {
        Write($"<color=#88CCFF>{enemyName}</color> <b>DODGE</b> | roll {roll:F1} under {dodgeChance:F0}% dodge");
    }

    public static void EnemyHardenedMitigation(string enemyName, float reduced, float hpAfter)
    {
        Write(
            $"<color={ColorFor(POIType.TurtleShell)}>{enemyName}</color> HARDENED | -{reduced:F0} dmg mitigated | {hpAfter:F0} HP");
    }

    public static void SteveFearMiss(float roll, float missChance)
    {
        Write($"<color={ColorForResidual("Fear")}>Steve FEAR miss</color> | roll {roll:F1} under {missChance:F0}% miss");
    }

    public static void SteveKnockback(float meters)
    {
        Write($"<color={ColorForResidual("Knockback")}>Steve KNOCKBACK</color> | {meters:F1}m");
    }

    public static void SteveCurseRoll()
    {
        Write($"<color={ColorForResidual("Curse")}>Steve CURSE</color> | rolling only 1 die this turn");
    }
}
