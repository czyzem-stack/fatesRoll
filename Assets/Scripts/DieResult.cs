using UnityEngine;

public class DieResult : MonoBehaviour
{
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    public bool IsSettled()
    {
        if (rb == null) return true;
        
        // Stricter settling threshold
        if (rb.IsSleeping()) return true;

        // Very strict threshold to ensure dice are truly still
        bool moving = rb.linearVelocity.sqrMagnitude > 0.0001f || rb.angularVelocity.sqrMagnitude > 0.0001f;
        return !moving;
    }

    void Update()
    {
        // Safety: If die falls through the floor, warp it back to Steve's feet or a safe height
        if (transform.position.y < -5.0f)
        {
            var hero = Object.FindAnyObjectByType<HeroController>();
            Vector3 safePos = hero != null ? hero.transform.position + Vector3.up * 2.0f : new Vector3(transform.position.x, 2.0f, transform.position.z);
            transform.position = safePos;
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            Debug.LogWarning("Die fell through floor! Warped back to safe height.");
        }
    }

    public int GetValue()
    {
        Vector3 up = Vector3.up;
float maxDot = -2f;
        int value = 0;

        // Visual Calibration Mapping (Fixed Z+/Z- swap):
        // Top (Y+) = 2 -> Down (Y-) = 5
        // Front (Z+) = 1 -> Back (Z-) = 6
        // Right (X+) = 4 -> Left (X-) = 3
        Vector3[] faceNormals = new Vector3[]
        {
            Vector3.up,       // 2
            Vector3.down,     // 5
            Vector3.forward,  // 1
            Vector3.back,     // 6
            Vector3.right,    // 4
            Vector3.left      // 3
        };
        int[] faceValues = new int[] { 2, 5, 1, 6, 4, 3 };

        string bestAxis = "None";

        for (int i = 0; i < faceNormals.Length; i++)
        {
            float dot = Vector3.Dot(transform.TransformDirection(faceNormals[i]), up);
            if (dot > maxDot)
            {
                maxDot = dot;
                value = faceValues[i];
                bestAxis = faceNormals[i].ToString();
            }
        }

        Debug.Log($"<color=cyan>DieResult: Detected {value}. Best Local Axis: {bestAxis} (Dot: {maxDot:F2})</color>");
        return value;
    }
}
