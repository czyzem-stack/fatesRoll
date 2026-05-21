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
        // In Unity 6, velocity is linearVelocity
        bool moving = rb.linearVelocity.sqrMagnitude > 0.002f || rb.angularVelocity.sqrMagnitude > 0.002f;
        return !moving;
    }

    public int GetValue()
    {
        Vector3 up = Vector3.up;
        float maxDot = -2f;
        int value = 0;

        // Face Normals for the white D6 model
        // We'll test standard orientation: 1-Up, 6-Down, etc.
        // If results are consistently wrong, we rotate the mapping here.
        Vector3[] faceNormals = new Vector3[]
        {
            Vector3.up,       // 1
            Vector3.down,     // 6
            Vector3.forward,  // 2
            Vector3.back,     // 5
            Vector3.right,    // 3
            Vector3.left      // 4
        };
        int[] faceValues = new int[] { 1, 6, 2, 5, 3, 4 };

        for (int i = 0; i < faceNormals.Length; i++)
        {
            float dot = Vector3.Dot(transform.TransformDirection(faceNormals[i]), up);
            if (dot > maxDot)
            {
                maxDot = dot;
                value = faceValues[i];
            }
        }

        Vector3 localUp = transform.InverseTransformDirection(Vector3.up);
        Debug.Log($"DieResult: Value {value} detected. Local Up: {localUp:F2}. Max Dot: {maxDot:F2}");
        return value;
    }
}
