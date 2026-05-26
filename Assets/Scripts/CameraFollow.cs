using System;
using UnityEngine;

/// <summary>Legacy follow camera. Prefer Cinemachine (IsometricCamera) instead.</summary>
[Obsolete("Use Cinemachine + IsometricCameraControl. This component is disabled by DiabloCameraSetup.")]
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0, 20, -10);
    public float smoothSpeed = 0.125f;
    public Vector3 rotation = new Vector3(60, 0, 0);

    void Start()
    {
        if (target == null)
        {
            var hero = GameObject.Find("/Steve/MC01");
            if (hero != null) target = hero.transform;
        }
        
        transform.rotation = Quaternion.Euler(rotation);
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + offset;
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;
    }
}
