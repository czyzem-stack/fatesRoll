using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Scales enemy primary stats from Steve's current build. Used for spawn slots and optional FTUE tuning.
/// </summary>
/// <remarks>Inherits <see cref="GameServiceBehaviour{T}"/> — auto-registers in Awake via <see cref="GameServices"/>.</remarks>
public class EnemyStatManager : GameServiceBehaviour<EnemyStatManager>
{

    [System.Serializable]
    public class EnemyStatDefinition
    {
        public POIType type;
        public float baseStrength = 8f;
        public float baseAgility = 8f;
        public float baseVitality = 8f;
        public float baseLuck = 5f;
        [Tooltip("Multiplied by Steve's stats when scaling (1.0 = match Steve's level).")]
        public float scalingMultiplier = 1.0f;
    }

    [Header("Enemy Specific Stats")]
    [SerializeField] private List<EnemyStatDefinition> enemyDefinitions = new List<EnemyStatDefinition>();
    [SerializeField] private EnemyStatDefinition defaultDefinition = new EnemyStatDefinition();

    [Header("Difficulty Scaling")]
    [Tooltip("Global base multiplier for scaling (100 = 100% of defined scaling).")]
    [Range(0f, 200f)]
    [SerializeField] private float difficultyPercent = 100f;

    [Tooltip("Added to difficulty % after each random-pool kill (infinite ramp).")]
    [SerializeField] private float difficultyPerRandomKill = 0f;

    [Header("FTUE (optional)")]
    [Tooltip("When no EnemyData on a sequential POI, scale Steve by this % times the step multiplier below.")]
    [SerializeField] private float ftueBasePercent = 50f;

    [SerializeField] private float ftuePercentPerVisitOrder = 8f;

    private float randomKillBonus;
    private int totalRandomKills;

    public float DifficultyPercent => difficultyPercent + randomKillBonus;

    public void NotifyRandomPoolKill()
    {
        randomKillBonus += difficultyPerRandomKill;
        totalRandomKills++;
    }

    public void ResetRunBonuses()
    {
        randomKillBonus = 0f;
        totalRandomKills = 0;
    }

    public void ApplyScaledStats(Enemy enemy)
    {
        if (enemy == null) return;

        POIType type = enemy.MonsterType;
        EnemyStatDefinition def = GetDefinition(type);

        // If no kills yet, use base stats for the initial world setup.
        if (totalRandomKills == 0)
        {
            enemy.strength = def.baseStrength;
            enemy.agility = def.baseAgility;
            enemy.vitality = def.baseVitality;
            enemy.luck = def.baseLuck;
        }
        else
        {
            // Respawned enemies scale with Steve's current power.
            if (!TryGetSteveStats(out float str, out float agi, out float vit, out float luck))
            {
                enemy.strength = def.baseStrength;
                enemy.agility = def.baseAgility;
                enemy.vitality = def.baseVitality;
                enemy.luck = def.baseLuck;
            }
            else
            {
                float globalScale = DifficultyPercent / 100f;
                float finalScale = globalScale * def.scalingMultiplier;

                enemy.strength = str * finalScale;
                enemy.agility = agi * finalScale;
                enemy.vitality = vit * finalScale;
                enemy.luck = luck * finalScale;
            }
        }

        enemy.Initialize();
    }

    private EnemyStatDefinition GetDefinition(POIType type)
    {
        foreach (var def in enemyDefinitions)
        {
            if (def.type == type)
            {
                // Safety: if the definition is all zeros (uninitialized in inspector), use default.
                if (def.baseStrength <= 0f && def.baseVitality <= 0f)
                    continue;
                return def;
            }
        }
        return defaultDefinition;
    }

    /// <summary>Sequential POI without authored EnemyData.</summary>
    public void ApplyFtueStepStats(Enemy enemy, int visitOrder)
    {
        if (enemy == null) return;

        POIType type = enemy.MonsterType;
        EnemyStatDefinition def = GetDefinition(type);

        if (!TryGetSteveStats(out float str, out float agi, out float vit, out float luck))
        {
            enemy.strength = def.baseStrength;
            enemy.agility = def.baseAgility;
            enemy.vitality = def.baseVitality;
            enemy.luck = def.baseLuck;
            enemy.Initialize();
            return;
        }

        float stepScale = (ftueBasePercent + visitOrder * ftuePercentPerVisitOrder) / 100f;
        float finalScale = stepScale * def.scalingMultiplier;

        enemy.strength = str * finalScale;
        enemy.agility = agi * finalScale;
        enemy.vitality = vit * finalScale;
        enemy.luck = luck * finalScale;
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
