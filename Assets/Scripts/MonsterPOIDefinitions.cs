using System.Collections.Generic;

/// <summary>Wave01 RPGMonsterWave01PBR monsters — prefabs live under CommonStuffs; animators under RPGMonsterWave01PBR.</summary>
public static class MonsterPOIDefinitions
{
    public const string PrefabRoot = "Assets/Monsters/CommonStuffs/Prefab/Wave01/CharacterPBR/";
    public const string AnimatorRoot = "Assets/Monsters/RPGMonsterWave01PBR/Animators/";

    public static readonly IReadOnlyList<POIType> CombatTypes = new[]
    {
        POIType.Orc,
        POIType.Skeleton,
        POIType.Slime,
        POIType.Bat,
        POIType.Dragon,
        POIType.EvilMage,
        POIType.Golem,
        POIType.MonsterPlant,
        POIType.Spider,
        POIType.TurtleShell
    };

    public static string GetPrefabAssetPath(POIType type)
    {
        switch (type)
        {
            case POIType.Orc: return PrefabRoot + "OrcPBRDefault.prefab";
            case POIType.Skeleton: return PrefabRoot + "SkeletonPBRDefault.prefab";
            case POIType.Slime: return PrefabRoot + "SlimePBRDefault.prefab";
            case POIType.Bat: return PrefabRoot + "BatPBRDefault.prefab";
            case POIType.Dragon: return PrefabRoot + "DragonPBRDefault.prefab";
            case POIType.EvilMage: return PrefabRoot + "EvilMagePBRDefault.prefab";
            case POIType.Golem: return PrefabRoot + "GolemPBRDefault.prefab";
            case POIType.MonsterPlant: return PrefabRoot + "MonsterPlantPBRDefault.prefab";
            case POIType.Spider: return PrefabRoot + "SpiderPBRDefault.prefab";
            case POIType.TurtleShell: return PrefabRoot + "TurtleShellPBR.prefab";
            default: return null;
        }
    }

    public static string GetAnimatorAssetPath(POIType type)
    {
        switch (type)
        {
            case POIType.Orc: return AnimatorRoot + "Orc.controller";
            case POIType.Skeleton: return "Assets/Animators/Skeleton_New.controller";
            case POIType.Slime: return AnimatorRoot + "Slime.controller";
            case POIType.Bat: return "Assets/Animators/BatAnimator.controller";
            case POIType.Dragon: return AnimatorRoot + "Dragon.controller";
            case POIType.EvilMage: return AnimatorRoot + "EvilMage.controller";
            case POIType.Golem: return AnimatorRoot + "Golem.controller";
            case POIType.MonsterPlant: return AnimatorRoot + "MonsterPlant.controller";
            case POIType.Spider: return AnimatorRoot + "Spider.controller";
            case POIType.TurtleShell: return AnimatorRoot + "TurtleShell.controller";
            default: return null;
        }
    }

    public static string GetDisplayName(POIType type)
    {
        switch (type)
        {
            case POIType.Orc: return "Orc";
            case POIType.Skeleton: return "Skeleton";
            case POIType.Slime: return "Slime";
            case POIType.Bat: return "Bat";
            case POIType.Dragon: return "Dragon";
            case POIType.EvilMage: return "Evil Mage";
            case POIType.Golem: return "Golem";
            case POIType.MonsterPlant: return "Monster Plant";
            case POIType.Spider: return "Spider";
            case POIType.TurtleShell: return "Turtle Shell";
            case POIType.TreasureChest: return "Treasure Chest";
            default: return type.ToString();
        }
    }
}
