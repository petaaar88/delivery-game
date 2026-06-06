using UnityEngine;

public class PackageDelivery : MonoBehaviour
{
    public float speedThreshold = 0.5f;

    private Collider _playerCollider;
    private Rigidbody _playerRigidbody;
    private VehiclePickupAnimator _vehicleAnimator;

    void Update()
    {
        if (_playerCollider == null)
            return;

        if (_playerRigidbody.linearVelocity.magnitude <= speedThreshold)
            TriggerDelivery();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        VehiclePickupAnimator animator = other.GetComponentInParent<VehiclePickupAnimator>();
        if (animator == null || !animator.HasPackage())
            return;

        _playerCollider = other;
        _playerRigidbody = other.GetComponentInParent<Rigidbody>();
        _vehicleAnimator = animator;
    }

    void OnTriggerExit(Collider other)
    {
        if (other == _playerCollider)
        {
            _playerCollider = null;
            _playerRigidbody = null;
            _vehicleAnimator = null;
        }
    }

    void TriggerDelivery()
    {
        if (!_vehicleAnimator.HasPackage())
            return;

        _vehicleAnimator.StartDeliverySequence();
        _playerCollider = null;
        _playerRigidbody = null;
        _vehicleAnimator = null;
    }
}
