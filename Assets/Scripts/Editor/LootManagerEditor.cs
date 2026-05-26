#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LootManager))]
public class LootManagerEditor : Editor
{
    static readonly GUIContent CoinCountHelp = new GUIContent(
        "Random number of coin pickups spawned when an enemy dies. Total gold = coin count × Gold Per Coin.");

    static readonly GUIContent FireworkHelp = new GUIContent(
        "Phase 1 — Firework burst: coins shoot outward from the enemy in all directions (not toward Steve), " +
        "arc through the air, and land in a ring around the corpse. Tune radius and arc for a bigger celebration.");

    static readonly GUIContent GroundHelp = new GUIContent(
        "Phase 2 — Ground celebration: after landing, coins rest on the ground with a slow spin and gentle bob. " +
        "Steve does not collect during this pause — this is the victory beat before the vacuum.");

    static readonly GUIContent PopHelp = new GUIContent(
        "Coin mesh prefab and size. Assign coin_04 if empty.");

    static readonly GUIContent PickupHelp = new GUIContent(
        "Phase 3 — Steve pickup: after the ground pause, coins fly to Steve one at a time. " +
        "Lower pickup speed and raise stagger for a slower, more satisfying collect.");

    static readonly GUIContent GoldHelp = new GUIContent(
        "Gold balance and floating reward text when a full batch reaches Steve.");

    static readonly GUIContent UiHelp = new GUIContent(
        "Link the HUD gold label. Right-click LootManager → Auto-Assign UI, or drag Text (TMP) here.");

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawSection("Coin Drop Count", CoinCountHelp);
        DrawField("minCoins");
        DrawField("maxCoins");

        DrawSection("Celebration — Firework Burst", FireworkHelp);
        DrawField("burstSpawnStagger");
        DrawField("popDuration");
        DrawField("popArcHeight");
        DrawField("fireworkRadiusMin");
        DrawField("fireworkRadiusMax");
        DrawField("spawnHeightOffset");

        DrawSection("Celebration — Ground Linger", GroundHelp);
        DrawField("groundCelebrateDuration");
        DrawField("groundSpinSpeed");
        DrawField("groundBobHeight");
        DrawField("groundBobSpeed");

        DrawSection("Pop & Land", PopHelp);
        DrawField("coinPrefab");
        DrawField("coinScale");

        DrawSection("Collection — Steve Pickup", PickupHelp);
        DrawField("stevePickupSpeed");
        DrawField("pickupStaggerDelay");
        DrawField("heroCollectOffset");

        DrawSection("Gold & UI", GoldHelp);
        DrawField("goldPerCoin");
        DrawField("goldFloatingTextColor");

        DrawSection("UI References", UiHelp);
        DrawField("goldText");

        EditorGUILayout.Space(8);
        if (GUILayout.Button("Auto-Assign Gold UI"))
        {
            ((LootManager)target).AutoAssignUI();
            EditorUtility.SetDirty(target);
        }

        serializedObject.ApplyModifiedProperties();
    }

    static void DrawSection(string title, GUIContent help)
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(help.text, MessageType.Info);
    }

    void DrawField(string propertyName)
    {
        SerializedProperty prop = serializedObject.FindProperty(propertyName);
        if (prop == null)
        {
            EditorGUILayout.HelpBox($"Missing property: {propertyName}", MessageType.Warning);
            return;
        }

        GUIContent label = new GUIContent(prop.displayName, GetTooltip(propertyName));
        EditorGUILayout.PropertyField(prop, label, true);
    }

    static string GetTooltip(string name)
    {
        if (Tooltips.TryGetValue(name, out string tip))
            return tip;
        return string.Empty;
    }

    static readonly System.Collections.Generic.Dictionary<string, string> Tooltips =
        new System.Collections.Generic.Dictionary<string, string>
        {
            { "minCoins", "Fewest coins spawned when an enemy dies." },
            { "maxCoins", "Most coins spawned per kill (inclusive). Random between Min and Max." },

            { "burstSpawnStagger", "Delay between each coin leaving the enemy. Use 0 for a single simultaneous pop." },
            { "popDuration", "How long each coin takes to fly from the enemy to its landing spot on the ground." },
            { "popArcHeight", "Peak height of the outward arc. Higher = more dramatic firework." },
            { "fireworkRadiusMin", "Closest horizontal distance (meters) a coin can land from the enemy center." },
            { "fireworkRadiusMax", "Farthest horizontal distance (meters) a coin can land — wider = bigger ring." },
            { "spawnHeightOffset", "Height above the enemy root where the burst originates (chest height)." },

            { "groundCelebrateDuration", "Seconds coins stay on the ground after landing before Steve starts collecting. Main celebration pause." },
            { "groundSpinSpeed", "Rotation speed (degrees/sec) while coins idle on the ground." },
            { "groundBobHeight", "How high/low coins bob while celebrating (meters). Keep small (e.g. 0.03–0.08)." },
            { "groundBobSpeed", "How fast the idle bob oscillates. Higher = quicker wiggle." },

            { "coinPrefab", "Mesh prefab for drops (default: Assets/Coins/Prefabs/Coins/coin_04)." },
            { "coinScale", "Uniform scale on each spawned coin. Increase if coins are hard to see." },

            { "stevePickupSpeed", "Travel speed (m/s) when a coin flies to Steve. Lower = slower vacuum. Speed ramps up gently." },
            { "pickupStaggerDelay", "Delay between each coin starting its flight to Steve — creates a one-by-one collect." },
            { "heroCollectOffset", "Offset from Steve's position where coins fly to (Y ≈ chest). X/Z usually 0." },

            { "goldPerCoin", "Gold added to the balance for each coin that reaches Steve." },
            { "goldFloatingTextColor", "Color of the world-space '+X Gold' text after the batch is collected." },
            { "goldText", "HUD TextMeshPro label for Steve's current gold total." },
        };
}
#endif
