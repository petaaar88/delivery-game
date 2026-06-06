using UnityEngine;

[RequireComponent(typeof(RCC_CarControllerV4))]
public class AirControl : MonoBehaviour
{
    [Header("Torque (deg/s^2)")]
    [SerializeField] private float pitchTorque = 180f;
    [SerializeField] private float rollTorque = 220f;

    [Header("Limits")]
    [SerializeField] private float maxAngularVelocity = 6f;
    [SerializeField] private float angularDrag = 1.5f;

    [Header("Invert")]
    [SerializeField] private bool invertPitch = false;
    [SerializeField] private bool invertRoll = false;

    private RCC_CarControllerV4 car;
    private Rigidbody rb;
    private float defaultAngularDrag;
    private float defaultMaxAngularVelocity;

    private void Awake()
    {
        car = GetComponent<RCC_CarControllerV4>();
        rb = car.Rigid;
        defaultAngularDrag = rb.angularDamping;
        defaultMaxAngularVelocity = rb.maxAngularVelocity;
    }

    private void FixedUpdate()
    {
        if (car.isGrounded)
        {
            rb.angularDamping = defaultAngularDrag;
            rb.maxAngularVelocity = defaultMaxAngularVelocity;
            return;
        }

        rb.maxAngularVelocity = Mathf.Max(defaultMaxAngularVelocity, maxAngularVelocity);
        rb.angularDamping = angularDrag;

        float pitch = car.throttleInput - car.brakeInput;
        float roll = car.steerInput;

        if (invertPitch) pitch = -pitch;
        if (invertRoll) roll = -roll;

        Vector3 torque = new Vector3(
            pitch * pitchTorque * Mathf.Deg2Rad,
            0f,
            -roll * rollTorque * Mathf.Deg2Rad
        );

        rb.AddRelativeTorque(torque, ForceMode.Acceleration);
    }
}
