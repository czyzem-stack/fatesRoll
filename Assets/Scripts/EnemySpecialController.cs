using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class EnemySpecialSettings
{
    public POIType enemyType;

    [Tooltip("Chance (0-100) to trigger the special ability.")]
    [Range(0, 100)] public float specialChance = 25f;

    [Tooltip("The strength of the effect.\n" +
             "- Orc: Damage multiplier (e.g., 1.5)\n" +
             "- Skeleton: Block chance (e.g., 50)\n" +
             "- Bat: Miss chance (e.g., 25)\n" +
             "- Dragon: Burn damage per turn (e.g., 10)\n" +
             "- EvilMage: Unused (Steve rolls ONE die)\n" +
             "- Slime: Regen HP % (e.g., 10)\n" +
             "- Golem: Knockback steps (e.g., 10)\n" +
             "- MonsterPlant: Poison DMG/turn (e.g., 8)\n" +
             "- Spider: Runaway steps (e.g., 7)\n" +
             "- TurtleShell: Damage reduction % (e.g., 50)")]
    public float effectValue = 25f;

    [Tooltip("Number of turns the effect lasts.")]
    public int buffTurns = 3;

    [Tooltip("Duration of the special animation or effect logic pause.")]
    public float effectDuration = 2f;

    [Tooltip("Text to display when the special ability triggers.")]
    public string floatingText;

    [Tooltip("Color of the floating text.")]
    public Color textColor = Color.white;
}

/// <summary>
/// Central hub for tuning enemy special abilities (Fear, Block, Burn, Curse, etc.)
/// instead of hardcoding values in Enemy or HeroController.
/// </summary>
public class EnemySpecialController : MonoBehaviour
{
    public List<EnemySpecialSettings> specialSettings = new List<EnemySpecialSettings>();

    private void Awake()
    {
        GameServices.Register(this);
    }

    private void OnDestroy()
    {
        GameServices.Unregister(this);
    }

    public EnemySpecialSettings GetSettings(POIType type)
    {
        return specialSettings.Find(s => s.enemyType == type);
    }

    public bool TryGetSettings(POIType type, out EnemySpecialSettings settings)
    {
        settings = GetSettings(type);
        return settings != null;
    }

    public float GetSpecialChance(POIType type, float fallback = 0f)
    {
        var s = GetSettings(type);
        return s != null ? s.specialChance : fallback;
    }

    public float GetEffectValue(POIType type, float fallback = 0f)
    {
        var s = GetSettings(type);
        return s != null ? s.effectValue : fallback;
    }

    public int GetBuffTurns(POIType type, int fallback = 3)
    {
        var s = GetSettings(type);
        return s != null ? s.buffTurns : fallback;
    }

    public float GetEffectDuration(POIType type, float fallback = 2.0f)
    {
        var s = GetSettings(type);
        return s != null ? s.effectDuration : fallback;
    }

    public string GetFloatingText(POIType type, string fallback = "")
    {
        var s = GetSettings(type);
        return s != null ? s.floatingText : fallback;
    }

    public Color GetTextColor(POIType type, Color fallback)
    {
        var s = GetSettings(type);
        return s != null ? s.textColor : fallback;
    }
}
