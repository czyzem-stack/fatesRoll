using System;
using UnityEngine;

/// <summary>
/// Obsolete placeholder so existing scenes keep a valid script reference.
/// Cinemachine + <see cref="IsometricCameraControl"/> drive the camera now.
/// </summary>
[Obsolete("Use Cinemachine + IsometricCameraControl. Remove via FatesRoll → Cleanup → Remove Obsolete Camera Follow.")]
[DisallowMultipleComponent]
[AddComponentMenu("")]
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0, 20, -10);
    public float smoothSpeed = 0.125f;
    public Vector3 rotation = new Vector3(60, 0, 0);
}
