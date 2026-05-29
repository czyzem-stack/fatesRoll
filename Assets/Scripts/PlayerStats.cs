using UnityEngine;

/// <summary>
/// A beginner-friendly RPG stats system inspired by Archero.
/// This script handles primary base stats and calculates secondary derived stats.
/// </summary>
[ExecuteAlways]
public class PlayerStats : MonoBehaviour
{
    /// <summary>Fired after derived stats are recalculated in play mode.</summary>
    public static event System.Action StatsChanged;
    [Header("Core Base Stats (Primary)")]
    
    [Tooltip("Strength: Increases physical power and melee/ranged damage")]
    public float strength = 10f;
    
    [Tooltip("Agility: Improves speed, reflexes, and evasion")]
    public float agility = 10f;
    
    [Tooltip("Vitality: Controls health and stamina/endurance")]
    public float vitality = 10f;
    
    [Tooltip("Luck: Affects critical hits and lucky outcomes")]
    public float luck = 10f;

    [Header("Current State")]
    public float currentHP;

    [Header("Equipment bonuses (applied by HeroEquipment)")]
    [SerializeField] private float equipmentStrength;
    [SerializeField] private float equipmentAgility;
    [SerializeField] private float equipmentVitality;
    [SerializeField] private float equipmentLuck;

    // Derived Stats (Secondary)
    [Header("Derived Stats (Read-Only)")]
    [SerializeField] private float maxHP;
    [SerializeField] private float attackDamage;
    [SerializeField] private float attackSpeed;
    [SerializeField] private float critChance;
    [SerializeField] private float critDamage;
    [SerializeField] private float dodgeChance;

    // Public getters for derived stats
    public float MaxHP => maxHP;
    public float AttackDamage => attackDamage;
    public float AttackSpeed => attackSpeed;
    public float CritChance => critChance;
    public float CritDamage => critDamage;
    public float DodgeChance => dodgeChance;

    public float PowerScore
    {
        get
        {
            float effStr = strength + equipmentStrength + talentStrength;
            float effAgi = agility + equipmentAgility + talentAgility;
            float effVit = vitality + equipmentVitality + talentVitality;
            float effLuck = luck + equipmentLuck + talentLuck;
            float sumBaseStats = effStr + effAgi + effVit + effLuck;
            return ((maxHP * (dodgeChance / 100f)) + sumBaseStats) * ((critChance + critDamage) / 100f);
        }
    }

    private void Start()
{
        CalculateAllDerivedStats();
        // Fully heal on start
        currentHP = maxHP;
    }

    private void Update()
    {
        // Only recalculate automatically in the Editor
        if (!Application.isPlaying)
        {
            CalculateAllDerivedStats();
        }
    }

    /// <summary>
    /// Resets current stats back to default base values.
    /// </summary>
    public void ResetStats()
    {
        strength = 10f;
        agility = 10f;
        vitality = 10f;
        luck = 10f;
        CalculateAllDerivedStats();
        currentHP = maxHP;
    }

    /// <summary>
    /// Calculates all secondary stats based on the core primary stats.
    /// This clearly shows how secondary stats are derived from core base stats.
    /// </summary>
    public void SetEquipmentBonuses(float str, float agi, float vit, float luckBonus)
    {
        equipmentStrength = str;
        equipmentAgility = agi;
        equipmentVitality = vit;
        equipmentLuck = luckBonus;
    }

    /// <summary>Primary stats including equipment — used by EnemyStatManager for scaling.</summary>
    public void GetEffectivePrimaries(out float str, out float agi, out float vit, out float luck)
    {
        str = strength + equipmentStrength;
        agi = agility + equipmentAgility;
        vit = vitality + equipmentVitality;
        luck = this.luck + equipmentLuck;
    }

    [Header("Talent bonuses (applied by TalentManager)")]
    public float talentStrength;
    public float talentAgility;
    public float talentVitality;
    public float talentLuck;
    public float talentMaxHP;
    public float talentDodge;
    public float talentCritChance;
    public float talentCritDamage;

    public void CalculateAllDerivedStats()
    {
        float effStr = strength + equipmentStrength + talentStrength;
        float effAgi = agility + equipmentAgility + talentAgility;
        float effVit = vitality + equipmentVitality + talentVitality;
        float effLuck = luck + equipmentLuck + talentLuck;

        // Max HP = Vitality * 10 + 100 + Talent Bonus
        maxHP = effVit * 10f + 100f + talentMaxHP;

        // Attack Damage = Strength * 4 + 20
        attackDamage = effStr * 4f + 20f;

        // Attack Speed = 1.0 + (Agility * 0.03)
        attackSpeed = 1.0f + (effAgi * 0.03f);

        // Crit Chance (%) = Luck * 0.8 + Talent Bonus
        critChance = effLuck * 0.8f + talentCritChance;

        // Crit Damage (%) = 50 + (Luck * 1.5) + Talent Bonus
        critDamage = 50f + (luck * 1.5f) + talentCritDamage;

        // Dodge Chance (%) = Agility * 0.6 + Talent Bonus
        dodgeChance = effAgi * 0.6f + talentDodge;

        // Ensure current HP doesn't exceed new Max HP
        if (currentHP > maxHP) currentHP = maxHP;

        if (Application.isPlaying)
            StatsChanged?.Invoke();
    }

    public void RestoreFullHealth()
    {
        CalculateAllDerivedStats();
        currentHP = maxHP;
    }

    /// <summary>
    /// Basic damage handling with dodge check.
    /// Returns true if damage was actually taken, false if dodged.
    /// </summary>
    public bool TakeDamage(float damage)
    {
        float dodgeRoll = Random.Range(0f, 100f);
        if (dodgeRoll < dodgeChance)
        {
            CombatLog.Dodge(gameObject.name, dodgeChance, dodgeRoll);
            return false;
        }

        float hpBefore = currentHP;
        currentHP -= damage;
        if (currentHP < 0) currentHP = 0;

        CombatLog.DamageTaken(gameObject.name, damage, hpBefore, currentHP);
        return true;
    }
}
