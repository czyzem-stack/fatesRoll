using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EnemyStatManager))]
public class EnemyStatManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox(
            "Enemy primaries = Steve's effective primaries × (Difficulty % / 100).\n" +
            "Tracks Steve as he levels/loots for infinite scaling. FTUE steps use the base/step fields when POI has no EnemyData.",
            MessageType.Info);
    }
}
