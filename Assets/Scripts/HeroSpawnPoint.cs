using UnityEngine;

/// <summary>Marks where Steve respawns after death. Place in the scene or leave unset to use Steve's start position.</summary>
public class HeroSpawnPoint : MonoBehaviour
{
    private void Awake()
    {
        if (!Application.isPlaying) return;
        SnapToPlaySpawnSurface();
    }

    public void SnapToPlaySpawnSurface()
    {
        PoiVisualPlacer.SnapToGround(transform);
        if (HeroSpawnUtility.TryResolveSpawnPosition(transform.position, out Vector3 resolved))
            transform.position = resolved;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.9f, 0.35f, 0.85f);
        Gizmos.DrawWireSphere(transform.position, 0.75f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 2f);
    }
#endif
}
