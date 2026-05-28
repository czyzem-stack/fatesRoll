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
    [Tooltip("Duration of the special animation or effect logic.")]
    public float effectDuration = 2f;
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
}
