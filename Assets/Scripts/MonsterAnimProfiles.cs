using UnityEngine;

/// <summary>Animation state names for RPGMonsterWave01 pack controllers (no Speed float).</summary>
public readonly struct MonsterAnimProfile
{
    public readonly bool useAnimatorParameters;
    public readonly bool useFlyLocomotion;
    public readonly string idleState;
    public readonly string walkState;
    public readonly string combatIdleState;
    public readonly string attackState;
    public readonly string getHitState;
    public readonly string dieState;
    public readonly string tauntState;

    public MonsterAnimProfile(
        bool useAnimatorParameters,
        bool useFlyLocomotion,
        string idleState,
        string walkState,
        string combatIdleState,
        string attackState,
        string getHitState = "GetHit",
        string dieState = "Die",
        string tauntState = "Taunting")
    {
        this.useAnimatorParameters = useAnimatorParameters;
        this.useFlyLocomotion = useFlyLocomotion;
        this.idleState = idleState;
        this.walkState = walkState;
        this.combatIdleState = combatIdleState;
        this.attackState = attackState;
        this.getHitState = getHitState;
        this.dieState = dieState;
        this.tauntState = tauntState;
    }

    public static MonsterAnimProfile Get(POIType type)
    {
        switch (type)
        {
            case POIType.Orc:
            case POIType.Skeleton:
            case POIType.Slime:
                return new MonsterAnimProfile(true, false, null, null, null, null);

            case POIType.Bat:
            case POIType.Dragon:
                return new MonsterAnimProfile(
                    false,
                    true,
                    "IdleNormal",
                    "FlyFWD",
                    "IdleBattle",
                    "Attack01");

            case POIType.MonsterPlant:
            case POIType.TurtleShell:
                return new MonsterAnimProfile(
                    false,
                    false,
                    "IdleNormal",
                    "WalkFWD",
                    "IdleBattle",
                    "Attack01",
                    tauntState: "Taunt");

            default:
                return new MonsterAnimProfile(
                    false,
                    false,
                    "IdleNormal",
                    "WalkFWD",
                    "IdleBattle",
                    "Attack01");
        }
    }
}
