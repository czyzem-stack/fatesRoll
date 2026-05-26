using UnityEngine;

/// <summary>Unity only persists root GameObjects with DontDestroyOnLoad.</summary>
public static class PersistenceUtility
{
    public static void DontDestroyOnLoadRoot(GameObject target)
    {
        if (target == null)
            return;

        if (target.transform.parent != null)
            target.transform.SetParent(null);

        Object.DontDestroyOnLoad(target);
    }
}
