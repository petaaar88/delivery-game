using UnityEngine;

public class FloatingRotation : MonoBehaviour
{
    [Header("Rotation")]
    [SerializeField] private Vector3 rotationSpeed = new Vector3(0f, 45f, 0f);

    [Header("Floating")]
    [SerializeField] private float floatHeight = 0.25f;
    [SerializeField] private float floatSpeed = 1f;

    private Vector3 startPosition;

    private void Start()
    {
        startPosition = transform.position;
    }

    private void Update()
    {
        // Rotate
        transform.Rotate(rotationSpeed * Time.deltaTime);

        // Float up and down
        float newY = startPosition.y + Mathf.Sin(Time.time * floatSpeed) * floatHeight;
        transform.position = new Vector3(startPosition.x, newY, startPosition.z);
    }
}