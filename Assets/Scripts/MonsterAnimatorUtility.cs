using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>Assigns the correct native animator controller per monster type (never the Orc rig on other avatars).</summary>
public static class MonsterAnimatorUtility
{
    public static void ApplyToVisual(GameObject visualRoot, POIType type, MonsterPrefabCatalog catalog = null)
    {
        if (visualRoot == null) return;

        var animator = visualRoot.GetComponentInChildren<Animator>();
        if (animator == null) return;

        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        RuntimeAnimatorController target = ResolveController(type, catalog);
        if (target != null)
            animator.runtimeAnimatorController = target;
    }

    public static RuntimeAnimatorController ResolveController(POIType type, MonsterPrefabCatalog catalog)
    {
        if (type == POIType.Orc)
        {
            if (catalog != null && catalog.gameplayFallback != null)
                return catalog.gameplayFallback;
            return LoadFallbackController();
        }

        if (catalog != null)
        {
            var fromCatalog = catalog.GetGameplayAnimator(type);
            if (fromCatalog != null)
                return fromCatalog;
        }

#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            MonsterPOIDefinitions.GetAnimatorAssetPath(type));
#else
        return null;
#endif
    }

    public static RuntimeAnimatorController LoadFallbackController()
    {
        var catalog = ResolveCatalog();
        if (catalog != null && catalog.gameplayFallback != null)
            return catalog.gameplayFallback;

#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            POIVisualBuilder.GameplayAnimatorFallbackPath);
#else
        return null;
#endif
    }

    public static MonsterPrefabCatalog ResolveCatalog()
    {
        var spawn = SpawnManager.Instance;
        return spawn != null ? spawn.MonsterCatalog : null;
    }
}
