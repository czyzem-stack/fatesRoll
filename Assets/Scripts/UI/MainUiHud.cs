using UnityEngine;

/// <summary>
/// Dual-path lookups for MainUI HUD widgets (Resources vs HUD_Resources, Profile vs HUD_Profile).
/// </summary>
public static class MainUiHud
{
    public const string Canvas = "MainUI_Canvas";
    public const string GlobalResources = "MainUI_Canvas/Resources";
    public const string GlobalResourcesLegacy = "MainUI_Canvas/HUD_Resources";
    public const string RollAttackButton = "MainUI_Canvas/HUD_Control/Joystick_Button_l_Attack";

    public static GameObject FindAlongPaths(params string[] paths)
    {
        if (paths == null)
            return null;

        foreach (string path in paths)
        {
            if (string.IsNullOrEmpty(path))
                continue;

            GameObject go = GameObject.Find(path);
            if (go != null)
                return go;
        }

        return null;
    }

    public static T FindComponentAlongPaths<T>(params string[] paths) where T : Component
    {
        GameObject go = FindAlongPaths(paths);
        return go != null ? go.GetComponent<T>() : null;
    }

    public static GameObject FindGlobalResourcesHud() =>
        FindAlongPaths(GlobalResources, GlobalResourcesLegacy);

    public static Transform FindMissionScrollContent(Transform panelRoot)
    {
        if (panelRoot == null)
            return null;

        Transform scroll = panelRoot.Find("ScrollRect");
        if (scroll == null)
            return null;

        Transform viewport = scroll.Find("Viewport") ?? scroll.Find("Veiwport");
        return viewport != null ? viewport.Find("Content") : null;
    }
}
