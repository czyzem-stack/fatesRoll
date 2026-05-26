#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RogueLiteManager))]
public class RogueLiteManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPropertiesExcluding(serializedObject,
            "m_Script",
            "strength", "agility", "vitality", "luck", "energyRegen", "coinBonus",
            "energyRegenTimeReduction", "bonusCoinsPerEnemyKill", "buttonPrefabs");

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Level-up stat offers", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Each stat: Upgrade Amount (+1, −1s, etc.), Offer Chance % (pool weight — 20 with all at 20 ≈ equal), " +
            "Upgrade Scaler (1 = flat; 1.1 scales repeat picks: +1, +1.1, +1.21…).",
            MessageType.Info);

        DrawStat("strength", "Strength", RogueLiteUpgradeType.Strength);
        DrawStat("agility", "Agility", RogueLiteUpgradeType.Agility);
        DrawStat("vitality", "Vitality", RogueLiteUpgradeType.Vitality);
        DrawStat("luck", "Luck", RogueLiteUpgradeType.Luck);
        DrawStat("energyRegen", "Energy regen", RogueLiteUpgradeType.EnergyRegen);
        DrawStat("coinBonus", "Coin bonus", RogueLiteUpgradeType.CoinBonus);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Button prefabs", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("buttonPrefabs"), true);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Runtime totals", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("energyRegenTimeReduction"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bonusCoinsPerEnemyKill"));
        }

        EditorGUILayout.Space(8f);
        if (GUILayout.Button("Assign Default Button Prefabs"))
        {
            var manager = (RogueLiteManager)target;
            Undo.RecordObject(manager, "Assign RogueLite button prefabs");
            manager.EditorAssignDefaultButtonPrefabs();
            EditorUtility.SetDirty(manager);
        }

        serializedObject.ApplyModifiedProperties();
    }

    void DrawStat(string propertyName, string label, RogueLiteUpgradeType type)
    {
        var prop = serializedObject.FindProperty(propertyName);
        if (prop == null)
            return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(prop.FindPropertyRelative("upgradeAmount"), new GUIContent("Upgrade Amount"));
        EditorGUILayout.PropertyField(prop.FindPropertyRelative("offerChancePercent"), new GUIContent("Offer Chance %"));
        EditorGUILayout.PropertyField(prop.FindPropertyRelative("upgradeScaler"), new GUIContent("Upgrade Scaler"));
        EditorGUILayout.PropertyField(prop.FindPropertyRelative("buttonColor"), new GUIContent("Button Color"));

        if (Application.isPlaying && target is RogueLiteManager manager)
        {
            float next = manager.GetNextBonusAmount(type);
            EditorGUILayout.LabelField("Next pick bonus", next.ToString("0.##"), EditorStyles.miniLabel);
        }
        else
        {
            float amount = prop.FindPropertyRelative("upgradeAmount").floatValue;
            float scaler = prop.FindPropertyRelative("upgradeScaler").floatValue;
            if (scaler <= 0f) scaler = 1f;
            float second = amount * scaler;
            EditorGUILayout.LabelField(
                "Preview",
                $"1st pick: {amount:0.##}   2nd pick: {second:0.##}   (×{scaler:0.##} scaler)",
                EditorStyles.miniLabel);
        }

        EditorGUI.indentLevel--;
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(2f);
    }
}
#endif
