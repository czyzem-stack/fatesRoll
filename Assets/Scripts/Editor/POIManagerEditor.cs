using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(POIManager))]
public class POIManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox(
            "Manages visit-order POIs only (any visit order you set).\n" +
            "When every visit POI is consumed, enables SpawnManager on SpawnNode markers.",
            MessageType.Info);
    }
}
