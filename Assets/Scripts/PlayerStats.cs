using UnityEngine;

/// <summary>
/// A beginner-friendly RPG stats system inspired by Archero.
/// This script handles primary base stats and calculates secondary derived stats.
/// </summary>
[ExecuteAlways]
public class PlayerStats : MonoBehaviour
{
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
    public void CalculateAllDerivedStats()
    {
        // Max HP = Vitality * 10 + 100
        // Vitality controls health and endurance
        maxHP = vitality * 10f + 100f;

        // Attack Damage = Strength * 4 + 20
        // Strength increases physical power
        attackDamage = strength * 4f + 20f;

        // Attack Speed = 1.0 + (Agility * 0.03)
        // Agility improves speed and reflexes
        attackSpeed = 1.0f + (agility * 0.03f);

        // Crit Chance (%) = Luck * 0.8
        // Luck affects critical hit probability
        critChance = luck * 0.8f;

        // Crit Damage (%) = 50 + (Luck * 1.5)
        // Luck also makes those critical strikes more powerful
        critDamage = 50f + (luck * 1.5f);

        // Dodge Chance (%) = Agility * 0.6
        // Agility improves evasion
        dodgeChance = agility * 0.6f;

        // Ensure current HP doesn't exceed new Max HP
        if (currentHP > maxHP) currentHP = maxHP;
    }

    public float GetFinalAttackDamage() => attackDamage;
    public float GetFinalCritChance() => critChance;
    public float GetFinalDodgeChance() => dodgeChance;

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
        // Dodge check: Agility based evasion
        if (Random.Range(0f, 100f) < dodgeChance)
        {
            Debug.Log($"<color=white>{gameObject.name} DODGED the attack!</color>");
            return false;
        }

        currentHP -= damage;
        if (currentHP < 0) currentHP = 0;
        
        Debug.Log($"<color=red>{gameObject.name} took {damage} damage. Current HP: {currentHP}</color>");
        return true;
    }
}
