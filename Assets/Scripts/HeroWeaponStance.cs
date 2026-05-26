using System;
using UnityEngine;

/// <summary>
/// Obsolete placeholder so existing scenes keep a valid script reference.
/// Remove this component from Steve via FatesRoll → Cleanup → Remove Obsolete Hero Components.
/// </summary>
[Obsolete("Removed during locomotion rebuild. Delete this component from the GameObject.")]
[DisallowMultipleComponent]
[AddComponentMenu("")]
public class HeroWeaponStance : MonoBehaviour
{
#if UNITY_EDITOR
    private void Reset()
    {
        Debug.LogWarning(
            "HeroWeaponStance is obsolete and does nothing. Use FatesRoll → Cleanup → Remove Obsolete Hero Components.",
            this);
    }
#endif
}
