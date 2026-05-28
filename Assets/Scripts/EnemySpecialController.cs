using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class EnemySpecialSettings
{
    public POIType enemyType;
    [Tooltip("Chance (0-100) to trigger the special ability.")]
    [Range(0, 100)] public float specialChance = 25f;
    [Tooltip("The strength of the effect (e.g., 25 for 25% miss chance).")]
    public float effectValue = 25f;
    [Tooltip("Number of turns the effect lasts.")]
    public int buffTurns = 3;
    [Tooltip("Duration of the special animation or effect logic.")]
    public float effectDuration = 2f;
    [Tooltip("Text to display when the special ability triggers.")]
    public string floatingText;
    [Tooltip("Color of the floating text.")]
    public Color textColor = Color.white;
}

/// <summary>
/// Central hub for tuning enemy special abilities (Fear, Block, etc.)
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
