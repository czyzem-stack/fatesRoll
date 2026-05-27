using UnityEngine;

/// <summary>Unified combat console output. Filter Console with: Combat</summary>
public static class CombatLog
{
    private const string Tag = "[Combat]";

    private static bool IsEnabled
    {
        get
        {
            var settings = GlobalSettings.Instance;
            return settings == null || settings.combatLogEnabled;
        }
    }

    public static void Info(string message)
    {
        if (!IsEnabled) return;
        Debug.Log($"{Tag} {message}");
    }

    public static void AttackStart(string attacker, string target, string attackType = "melee")
    {
        if (!IsEnabled) return;
        Debug.Log($"{Tag} {attacker} -> {target} | attack start ({attackType})");
    }

    public static void Dodge(string defender, float dodgeChance, float roll)
    {
        if (!IsEnabled) return;
        Debug.Log($"{Tag} DODGE {defender} | roll {roll:F1} under {dodgeChance:F1}% dodge");
    }

    public static void CritCheck(string attacker, float critChance, float roll, bool isCrit)
    {
        if (!IsEnabled) return;
        if (isCrit)
            Debug.Log($"{Tag} CRIT {attacker} | roll {roll:F1} under {critChance:F1}%");
        else
            Debug.Log($"{Tag} {attacker} crit check | roll {roll:F1} vs {critChance:F1}% — no crit");
    }

    public static void DamageDealt(string attacker, string target, float amount, float targetHpAfter, bool crit = false)
    {
        if (!IsEnabled) return;
        string critLabel = crit ? " CRIT" : "";
        Debug.Log($"{Tag} HIT{critLabel} {attacker} -> {target} | {amount:F0} dmg | HP left {targetHpAfter:F0}");
    }

    public static void DamageTaken(string target, float amount, float hpBefore, float hpAfter)
    {
        if (!IsEnabled) return;
        Debug.Log($"{Tag} DAMAGE {target} | -{amount:F0} | HP {hpBefore:F0} -> {hpAfter:F0}");
    }

    public static void DamageMitigated(string attacker, string target, string reason)
    {
        if (!IsEnabled) return;
        Debug.Log($"{Tag} MISS {attacker} -> {target} | {reason}");
    }

    public static void DamageCalc(string attacker, string detail)
    {
        if (!IsEnabled) return;
        Debug.Log($"{Tag} {attacker} damage calc | {detail}");
    }

    public static void Death(string unit)
    {
        if (!IsEnabled) return;
        Debug.Log($"{Tag} DEFEATED {unit}");
    }

    public static void EnterCombat(string hero, string enemy)
    {
        if (!IsEnabled) return;
        Debug.Log($"{Tag} Engaged | {hero} vs {enemy}");
    }

    /// <summary>
    /// Rolls for crit and applies the multiplier if successful. Returns final damage and whether it crit.
    /// </summary>
    public static float RollAndApplyCrit(float baseDamage, float critChance, float critMultiplierPercent, out bool isCrit)
    {
        isCrit = false;
        if (critChance <= 0f) return baseDamage;

        float roll = Random.Range(0f, 100f);
        isCrit = roll < critChance;

        if (isCrit)
            return baseDamage * (1f + critMultiplierPercent / 100f);

        return baseDamage;
    }

    /// <summary>Applies crit multiplier only if the flag is true.</summary>
    public static float ApplyCritMultiplier(float baseDamage, bool isCrit, float critMultiplierPercent)
    {
        return isCrit ? baseDamage * (1f + critMultiplierPercent / 100f) : baseDamage;
    }
}
