using UnityEngine;

[RequireComponent(typeof(RCC_CarControllerV4))]
public class FlipRecovery : MonoBehaviour
{
    [Header("Detection")]
    [Tooltip("Vehicle is considered overturned when transform.up · world up is below this value.")]
    [SerializeField, Range(-1f, 1f)] private float overturnedDotThreshold = 0.3f;
    [Tooltip("Only flip when linear speed (m/s) is below this value.")]
    [SerializeField] private float maxSpeedForRecovery = 2f;
    [Tooltip("Seconds the vehicle must stay overturned before auto-flipping.")]
    [SerializeField] private float overturnedTimeBeforeFlip = 2f;

    [Header("Flip")]
    [Tooltip("Extra height (meters) to lift the vehicle when flipping to avoid clipping.")]
    [SerializeField] private float liftOnFlip = 1f;

    private RCC_CarControllerV4 car;
    private Rigidbody rb;
    private float overturnedTimer;

    private void Awake()
    {
        car = GetComponent<RCC_CarControllerV4>();
        rb = car.Rigid;
    }

    private void FixedUpdate()
    {
        float upDot = Vector3.Dot(transform.up, Vector3.up);
        bool overturned = upDot < overturnedDotThreshold;
        bool slowEnough = rb.linearVelocity.sqrMagnitude <= maxSpeedForRecovery * maxSpeedForRecovery;

        if (!overturned || !slowEnough)
        {
            overturnedTimer = 0f;
            return;
        }

        overturnedTimer += Time.fixedDeltaTime;
        if (overturnedTimer < overturnedTimeBeforeFlip)
            return;

        Flip();
        overturnedTimer = 0f;
    }

    private void Flip()
    {
        float yaw = transform.eulerAngles.y;
        Vector3 position = transform.position + Vector3.up * liftOnFlip;
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.position = position;
        rb.rotation = rotation;
        transform.SetPositionAndRotation(position, rotation);
    }
}
