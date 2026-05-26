using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.InputSystem;

public class IsometricCameraControl : MonoBehaviour
{
    [Header("Zoom Settings")]
    public float minDistance = 15f;
    public float maxDistance = 28f;
    public float zoomSpeed = 2f;
    public float zoomSmoothTime = 0.08f;
    public Vector2 lockedRotation = new Vector2(35f, 45f);

    private CinemachineCamera vcam;
    private CinemachinePositionComposer composer;
    private float zoomVelocity;
    private float targetDistance;

    void Start()
    {
        vcam = GetComponent<CinemachineCamera>();
        composer = GetComponent<CinemachinePositionComposer>();
        if (composer != null)
        {
            targetDistance = composer.CameraDistance;
        }
        ApplyLockedRotation();
    }

    void LateUpdate()
    {
        if (composer == null)
        {
            return;
        }

        // Mouse Wheel Zoom
        float scroll = 0f;
        if (Mouse.current != null)
        {
            scroll = Mouse.current.scroll.ReadValue().y;
        }
        else
        {
            scroll = Input.mouseScrollDelta.y * 120f;
        }

        if (Mathf.Abs(scroll) > 0.01f)
        {
            targetDistance -= scroll * 0.01f * zoomSpeed;
            targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
        }

        composer.CameraDistance = Mathf.SmoothDamp(
            composer.CameraDistance,
            targetDistance,
            ref zoomVelocity,
            zoomSmoothTime);

        // Hard lock to Diablo angle.
        ApplyLockedRotation();
    }

    [ContextMenu("Trigger Shake")]
    public void TestShake()
    {
        var source = GetComponent<CinemachineImpulseSource>();
        if (source != null)
        {
            source.GenerateImpulse();
        }
    }

    private void ApplyLockedRotation()
    {
        transform.rotation = Quaternion.Euler(lockedRotation.x, lockedRotation.y, 0f);
    }
}
