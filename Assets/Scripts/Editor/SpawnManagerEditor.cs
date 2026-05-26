using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SpawnManager))]
public class SpawnManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox(
            "Uses SpawnNode markers — not POIs.\n" +
            "Fills monsters and treasure chests on load (Fill Spawns On Load).\n" +
            "Spawn Chest Chance rolls chests on monster nodes; use Treasure Chest spawn nodes for guaranteed chests.\n" +
            "Steve only paths to spawns after all visit POIs are cleared.",
            MessageType.Info);
    }
}
