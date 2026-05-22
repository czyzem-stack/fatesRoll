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

        bool moving = rb.linearVelocity.sqrMagnitude > 0.000005f || rb.angularVelocity.sqrMagnitude > 0.000005f;
        return !moving;
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
