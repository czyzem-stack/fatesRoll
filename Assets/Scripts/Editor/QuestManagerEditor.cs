#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Avoids Unity 6 default ListView on nested Quest lists (SerializedObject NRE in inspector).
/// </summary>
[CustomEditor(typeof(QuestManager))]
public class QuestManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if (target == null || serializedObject == null || serializedObject.targetObject == null)
            return;

        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("hasInitializedRunData"));

        var manager = (QuestManager)target;
        int questCount = manager.activeQuests != null ? manager.activeQuests.Count : 0;
        int achievementCount = manager.achievements != null ? manager.achievements.Count : 0;

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Runtime data", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Quest lists are built at runtime (Mission panel). " +
            "The default list inspector is hidden to prevent editor errors.",
            MessageType.None);
        EditorGUILayout.LabelField("Active quests", questCount.ToString());
        EditorGUILayout.LabelField("Achievements", achievementCount.ToString());

        if (GUILayout.Button("Reset and Generate Quests"))
        {
            Undo.RecordObject(manager, "Reset Quest Data");
            manager.GenerateInitialData();
            EditorUtility.SetDirty(manager);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
