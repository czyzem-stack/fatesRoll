using UnityEngine;

/// <summary>Snaps POI roots to ground and aligns prop pivots (e.g. treasure chest) so the mesh sits on the surface.</summary>
public static class PoiVisualPlacer
{
    public static bool SnapToGround(Transform poiRoot, float rayStartHeight = 40f, float maxDistance = 120f)
    {
        if (poiRoot == null)
            return false;

        Vector3 origin = poiRoot.position + Vector3.up * rayStartHeight;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, maxDistance, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore))
            return false;

        poiRoot.position = hit.point;
        return true;
    }

    /// <summary>Moves a visual child so the lowest renderer bounds sit on the POI root Y (ground).</summary>
    public static void AlignVisualBottomToPoiRoot(Transform poiRoot, GameObject visual)
    {
        if (poiRoot == null || visual == null)
            return;

        var renderers = visual.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        float lift = poiRoot.position.y - bounds.min.y;
        if (Mathf.Abs(lift) < 0.001f)
            return;

        visual.transform.position += Vector3.up * lift;
    }

    /// <summary>Snap POI root to ground and align the monster mesh so its feet sit on the surface.</summary>
    public static bool PlaceEnemyOnGround(Transform poiRoot, GameObject visual)
    {
        if (!SnapToGround(poiRoot))
            return false;

        if (visual != null)
            AlignVisualBottomToPoiRoot(poiRoot, visual);

        return true;
    }

    public static void PlaceTreasureChestVisual(Transform poiRoot, GameObject visual)
    {
        if (visual == null)
            return;

        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;
        visual.transform.localPosition = Vector3.zero;
        AlignVisualBottomToPoiRoot(poiRoot, visual);
    }
}
