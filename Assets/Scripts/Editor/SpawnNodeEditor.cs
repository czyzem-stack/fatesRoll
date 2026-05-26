using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SpawnNode))]
public class SpawnNodeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox(
            "Random spawn location. Monster nodes get combat or a rolled treasure chest (see SpawnManager).\n" +
            "Treasure Chest kind always spawns an equipment chest.",
            MessageType.Info);

        if (GUILayout.Button("Snap To Ground"))
        {
            var node = (SpawnNode)target;
            PoiVisualPlacer.SnapToGround(node.transform);
            EditorUtility.SetDirty(node);
        }
    }
}
