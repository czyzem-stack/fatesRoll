using UnityEngine;

/// <summary>Unified combat console output. Filter Console with: Combat</summary>
public static class CombatLog
{
    private const string Tag = "[Combat]";

    public static void Info(string message)
    {
        Debug.Log($"{Tag} {message}");
    }

    public static void AttackStart(string attacker, string target, string attackType = "melee")
    {
        Debug.Log($"{Tag} {attacker} -> {target} | attack start ({attackType})");
    }

    public static void Dodge(string defender, float dodgeChance, float roll)
    {
        Debug.Log($"{Tag} DODGE {defender} | roll {roll:F1} under {dodgeChance:F1}% dodge");
    }

    public static void CritCheck(string attacker, float critChance, float roll, bool isCrit)
    {
        if (isCrit)
            Debug.Log($"{Tag} CRIT {attacker} | roll {roll:F1} under {critChance:F1}%");
        else
            Debug.Log($"{Tag} {attacker} crit check | roll {roll:F1} vs {critChance:F1}% — no crit");
    }

    public static void DamageDealt(string attacker, string target, float amount, float targetHpAfter, bool crit = false)
    {
        string critLabel = crit ? " CRIT" : "";
        Debug.Log($"{Tag} HIT{critLabel} {attacker} -> {target} | {amount:F0} dmg | HP left {targetHpAfter:F0}");
    }

    public static void DamageTaken(string target, float amount, float hpBefore, float hpAfter)
    {
        Debug.Log($"{Tag} DAMAGE {target} | -{amount:F0} | HP {hpBefore:F0} -> {hpAfter:F0}");
    }

    public static void DamageMitigated(string attacker, string target, string reason)
    {
        Debug.Log($"{Tag} MISS {attacker} -> {target} | {reason}");
    }

    public static void DamageCalc(string attacker, string detail)
    {
        Debug.Log($"{Tag} {attacker} damage calc | {detail}");
    }

    public static void Death(string unit)
    {
        Debug.Log($"{Tag} DEFEATED {unit}");
    }

    public static void EnterCombat(string hero, string enemy)
    {
        Debug.Log($"{Tag} Engaged | {hero} vs {enemy}");
    }
}
