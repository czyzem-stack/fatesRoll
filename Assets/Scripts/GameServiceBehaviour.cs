using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene service registered with <see cref="GameServices"/> in Awake — not Start.
/// No static instance field (avoids stale GC handles after script domain reload).
/// </summary>
public abstract class GameServiceBehaviour<T> : MonoBehaviour where T : GameServiceBehaviour<T>
{
    /// <summary>Returns null if not yet registered (safe for null checks). Use <see cref="GameServices.Get{T}"/> when required.</summary>
    public static T Instance => GameServices.TryGet(out T service) ? service : null;

    public static bool HasInstance => GameServices.TryGet<T>(out _);

    protected virtual void Awake()
    {
        TryRegisterWithBootstrap();
    }

    protected virtual void Start()
    {
        TryRegisterWithBootstrap();
    }

    bool TryRegisterWithBootstrap()
    {
        if (GameServices.TryGet(out T existing) && existing != null && !ReferenceEquals(existing, this))
        {
            // Bootstrap / DDOL owns the singleton; gameplay scenes often still carry legacy copies — not an error.
            if (IsDontDestroyOnLoadScene(existing.gameObject.scene))
                Debug.Log($"{typeof(T).Name}: bootstrap already owns this service — destroying duplicate '{name}'.", this);
            else
                Debug.LogWarning($"Duplicate {typeof(T).Name} on '{name}' — destroying.", this);
            Destroy(this);
            return false;
        }

        if (GameServices.Current == null)
            return true;

        GameServices.Register((T)this);
        return true;
    }

    protected virtual void OnDestroy()
    {
        // Central place to stop coroutines for all services. Derived classes can still
        // override and call base.OnDestroy() after their own cleanup.
        StopAllCoroutines();
        GameServices.Unregister((T)this);
    }

    static bool IsDontDestroyOnLoadScene(Scene scene) =>
        scene.IsValid() && scene.name == "DontDestroyOnLoad";
}
