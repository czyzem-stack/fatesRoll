#if UNITY_EDITOR
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DiabloCameraSetup
{
    [MenuItem("FatesRoll/Camera/Setup Diablo IV Isometric Camera")]
    public static void SetupDiabloCamera()
    {
        Transform player = FindPlayerTransform();

        Camera mainCamera = EnsureMainCamera();
        EnsureMainCameraDefaults(mainCamera);

        GameObject vcamObject = GameObject.Find("Diablo Isometric Cam");
        if (vcamObject == null)
        {
            vcamObject = new GameObject("Diablo Isometric Cam");
        }

        Undo.RegisterCreatedObjectUndo(vcamObject, "Setup Diablo Isometric Camera");

        CinemachineCamera vcam = GetOrAddComponent<CinemachineCamera>(vcamObject);
        vcam.Priority = 10;
        vcam.Follow = player;
        vcam.Lens.FieldOfView = 45f;
        vcamObject.transform.rotation = Quaternion.Euler(35f, 45f, 0f);

        CinemachinePositionComposer composer = GetOrAddComponent<CinemachinePositionComposer>(vcamObject);
        composer.CameraDistance = 20f;
        composer.Damping = new Vector3(0.4f, 0.4f, 0.4f);

        // Remove any aim composer to keep a strict fixed isometric angle.
        CinemachineRotationComposer rotationComposer = vcamObject.GetComponent<CinemachineRotationComposer>();
        if (rotationComposer != null)
        {
            Undo.DestroyObjectImmediate(rotationComposer);
        }

        IsometricCameraControl control = GetOrAddComponent<IsometricCameraControl>(vcamObject);
        control.minDistance = 15f;
        control.maxDistance = 28f;
        control.zoomSpeed = 2f;
        control.lockedRotation = new Vector2(35f, 45f);

        // Subtle shake support.
        GetOrAddComponent<CinemachineImpulseListener>(mainCamera.gameObject);
        GetOrAddComponent<CinemachineImpulseSource>(vcamObject);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = vcamObject;

        if (player == null)
        {
            Debug.LogWarning("Diablo camera setup complete, but no player transform was found. Assign Follow manually.");
        }
        else
        {
            Debug.Log($"Diablo camera setup complete. Following: {player.name}");
        }
    }

    private static Camera EnsureMainCamera()
    {
        GameObject go = GameObject.Find("Main Camera");
        if (go == null)
        {
            go = new GameObject("Main Camera");
            Undo.RegisterCreatedObjectUndo(go, "Create Main Camera");
            go.AddComponent<AudioListener>();
        }

        Camera cam = go.GetComponent<Camera>();
        if (cam == null)
        {
            cam = go.AddComponent<Camera>();
        }

        return cam;
    }

    private static void EnsureMainCameraDefaults(Camera cam)
    {
        cam.gameObject.tag = "MainCamera";
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.orthographic = false;
        cam.fieldOfView = 45f;

        GetOrAddComponent<CinemachineBrain>(cam.gameObject);

        // Disable legacy camera follow to avoid conflicts (avoid referencing obsolete CameraFollow).
        DisableComponentByTypeName(cam.gameObject, "CameraFollow");
    }

    private static void DisableComponentByTypeName(GameObject go, string typeName)
    {
        MonoBehaviour[] behaviours = go.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour mb = behaviours[i];
            if (mb != null && mb.GetType().Name == typeName)
            {
                mb.enabled = false;
                return;
            }
        }
    }

    private static Transform FindPlayerTransform()
    {
        HeroController hero = Object.FindAnyObjectByType<HeroController>();
        if (hero != null)
        {
            return hero.transform;
        }

        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (taggedPlayer != null)
        {
            return taggedPlayer.transform;
        }

        GameObject steve = GameObject.Find("Steve");
        if (steve != null)
        {
            return steve.transform;
        }

        return null;
    }

    private static T GetOrAddComponent<T>(GameObject go) where T : Component
    {
        T comp = go.GetComponent<T>();
        if (comp == null)
        {
            comp = Undo.AddComponent<T>(go);
        }
        return comp;
    }
}
#endif
