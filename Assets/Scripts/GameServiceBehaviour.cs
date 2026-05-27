using UnityEngine;

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
            Debug.LogWarning($"Duplicate {typeof(T).Name} on '{name}' — destroying.", this);
            Destroy(gameObject);
            return false;
        }

        if (GameServices.Current == null)
            return true;

        GameServices.Register((T)this);
        return true;
    }

    protected virtual void OnDestroy()
    {
        GameServices.Unregister((T)this);
    }
}
