using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(POINode))]
public class POINodeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        POINode node = (POINode)target;

        EditorGUI.BeginChangeCheck();
        
        // Explicitly show the enum dropdown
        node.type = (POIType)EditorGUILayout.EnumPopup("POI Type", node.type);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(node, "Change POI Type");
            node.RefreshVisuals();
            EditorUtility.SetDirty(node);
        }

        if (GUILayout.Button("Manual Refresh"))
        {
            node.RefreshVisuals();
        }
    }
}
