using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(POINode))]
public class POINodeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        POINode node = (POINode)target;

        EditorGUI.BeginChangeCheck();

        node.type = (POIType)EditorGUILayout.EnumPopup("Enemy Type", node.type);
        node.order = EditorGUILayout.IntField("Visit Order", node.order);

        if (!node.IsTreasureChest)
        {
            EditorGUILayout.Space();
            node.enemyData = (EnemyData)EditorGUILayout.ObjectField("Enemy Data asset", node.enemyData, typeof(EnemyData), false);
            
            node.useManualStats = EditorGUILayout.Toggle("Use Manual Stats", node.useManualStats);
            if (node.useManualStats)
            {
                EditorGUI.indentLevel++;
                node.baseStrength = EditorGUILayout.FloatField("Base Strength", node.baseStrength);
                node.baseAgility = EditorGUILayout.FloatField("Base Agility", node.baseAgility);
                node.baseVitality = EditorGUILayout.FloatField("Base Vitality", node.baseVitality);
                node.baseLuck = EditorGUILayout.FloatField("Base Luck", node.baseLuck);
                EditorGUI.indentLevel--;
            }
        }

        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(node);
            if (node.type != POIType.TreasureChest)
                node.isTreasureChest = false;
            else
                node.isTreasureChest = true;
            POIVisualBuilder.BuildVisuals(node);
        }

        EditorGUILayout.HelpBox(
            "Visit order is fully flexible (0, 1, 2 … 15 …). Steve follows visit order until every POI is cleared. " +
            "Random infinite spawns use SpawnNode markers, not POIs.",
            MessageType.Info);

        if (node.IsTreasureChest)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Treasure chest loot", EditorStyles.boldLabel);
            node.ftueLootIndex = EditorGUILayout.IntField("FTUE Loot Index (-1 = random)", node.ftueLootIndex);
            node.ftueForcedOptionA = (EquipmentItemDefinition)EditorGUILayout.ObjectField(
                "FTUE Forced Option A", node.ftueForcedOptionA, typeof(EquipmentItemDefinition), false);
            node.ftueForcedOptionB = (EquipmentItemDefinition)EditorGUILayout.ObjectField(
                "FTUE Forced Option B", node.ftueForcedOptionB, typeof(EquipmentItemDefinition), false);
        }

        if (GUILayout.Button("Force Refresh Visuals"))
            POIVisualBuilder.BuildVisuals(node);

        if (GUILayout.Button("Force To Ground"))
            SnapNodeToGround(node);
    }

    private static void SnapNodeToGround(POINode node)
    {
        if (node.IsTreasureChest)
        {
            if (!PoiVisualPlacer.SnapToGround(node.transform)) return;
            if (node.currentVisual != null)
                PoiVisualPlacer.PlaceTreasureChestVisual(node.transform, node.currentVisual);
        }
        else if (!PoiVisualPlacer.PlaceEnemyOnGround(node.transform, node.currentVisual))
        {
            Debug.LogWarning($"Force To Ground: no hit under {node.name}.");
            return;
        }

        EditorUtility.SetDirty(node);
    }

    public static void RefreshVisuals(POINode node) => POIVisualBuilder.BuildVisuals(node);
}
