using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[CustomEditor(typeof(EnemyStatManager))]
public class EnemyStatManagerEditor : Editor
{
    private SerializedProperty enemyDefinitions;
    private SerializedProperty defaultDefinition;
    private SerializedProperty difficultyPercent;
    private SerializedProperty difficultyPerRandomKill;
    private SerializedProperty ftueBasePercent;
    private SerializedProperty ftuePercentPerVisitOrder;
    
    private Vector2 scrollPos;

    private void OnEnable()
    {
        enemyDefinitions = serializedObject.FindProperty("enemyDefinitions");
        defaultDefinition = serializedObject.FindProperty("defaultDefinition");
        difficultyPercent = serializedObject.FindProperty("difficultyPercent");
        difficultyPerRandomKill = serializedObject.FindProperty("difficultyPerRandomKill");
        ftueBasePercent = serializedObject.FindProperty("ftueBasePercent");
        ftuePercentPerVisitOrder = serializedObject.FindProperty("ftuePercentPerVisitOrder");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "POIType (Point of Interest Type) is the identifier for every entity in the game.\n" +
            "It maps a name (Orc, Bat, Skeleton) to its visuals, animations, and these stats.",
            MessageType.None);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Global Scaling Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(difficultyPercent);
        EditorGUILayout.PropertyField(difficultyPerRandomKill);
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("FTUE / Tutorial Scaling", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(ftueBasePercent);
        EditorGUILayout.PropertyField(ftuePercentPerVisitOrder);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Enemy Specific Stats", EditorStyles.boldLabel);
        
        // Use a scrolling area for the enemy list to avoid a massive inspector
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(300));
        EditorGUILayout.BeginVertical("box");

        for (int i = 0; i < enemyDefinitions.arraySize; i++)
        {
            SerializedProperty def = enemyDefinitions.GetArrayElementAtIndex(i);
            DrawDefinition(def, i);
        }

        if (GUILayout.Button("Add New Enemy Definition"))
        {
            enemyDefinitions.InsertArrayElementAtIndex(enemyDefinitions.arraySize);
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Default Fallback (If type is missing)", EditorStyles.boldLabel);
        DrawDefinition(defaultDefinition, -1);

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Initial Batch (0 kills): Uses 'Base Stats'.\n" +
            "Respawns (1+ kills): Steve's Stats × (Difficulty % / 100) × Scaling Multiplier.",
            MessageType.Info);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawDefinition(SerializedProperty def, int index)
    {
        EditorGUILayout.BeginVertical("helpbox");
        
        EditorGUILayout.BeginHorizontal();
        SerializedProperty typeProp = def.FindPropertyRelative("type");
        EditorGUILayout.PropertyField(typeProp, GUIContent.none, GUILayout.Width(120));
        GUILayout.FlexibleSpace();
        
        if (index >= 0 && GUILayout.Button("X", GUILayout.Width(20)))
        {
            enemyDefinitions.DeleteArrayElementAtIndex(index);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }
        EditorGUILayout.EndHorizontal();

        // Draw stats in a compact grid-like layout
        EditorGUILayout.BeginHorizontal();
        DrawStat(def.FindPropertyRelative("baseStrength"), "STR");
        DrawStat(def.FindPropertyRelative("baseAgility"), "AGI");
        DrawStat(def.FindPropertyRelative("baseVitality"), "VIT");
        DrawStat(def.FindPropertyRelative("baseLuck"), "LCK");
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.PropertyField(def.FindPropertyRelative("scalingMultiplier"), new GUIContent("Scale Mult"));

        EditorGUILayout.EndVertical();
    }

    private void DrawStat(SerializedProperty prop, string label)
    {
        float originalLabelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 35;
        EditorGUILayout.PropertyField(prop, new GUIContent(label));
        EditorGUIUtility.labelWidth = originalLabelWidth;
    }
}
