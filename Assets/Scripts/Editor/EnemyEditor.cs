using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Enemy))]
public class EnemyEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        if (GUILayout.Button("Force To Ground"))
            SnapEnemyToGround((Enemy)target);
    }

    private static void SnapEnemyToGround(Enemy enemy)
    {
        if (enemy == null) return;

        var poi = enemy.GetComponent<POINode>();
        Transform root = poi != null ? poi.transform : enemy.transform;
        GameObject visual = poi != null ? poi.currentVisual : FindPrimaryVisual(enemy.gameObject);

        bool snapped = poi != null
            ? PoiVisualPlacer.PlaceEnemyOnGround(root, visual)
            : PoiVisualPlacer.SnapToGround(root);

        if (!snapped)
        {
            Debug.LogWarning($"Snap To Ground: no ground hit under {root.name}. Check colliders and position.");
            return;
        }

        if (poi == null && visual != null)
            PoiVisualPlacer.AlignVisualBottomToPoiRoot(root, visual);

        EditorUtility.SetDirty(enemy);
        if (poi != null)
            EditorUtility.SetDirty(poi);
    }

    private static GameObject FindPrimaryVisual(GameObject root)
    {
        Renderer best = null;
        float bestVolume = 0f;

        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (r is SpriteRenderer) continue;
            float volume = r.bounds.size.sqrMagnitude;
            if (volume > bestVolume)
            {
                bestVolume = volume;
                best = r;
            }
        }

        return best != null ? best.gameObject : null;
    }
}
