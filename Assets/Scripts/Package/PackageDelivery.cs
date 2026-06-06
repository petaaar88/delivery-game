using System.Collections;
using UnityEngine;

public class PackageDelivery : MonoBehaviour
{
    public float speedThreshold = 0.5f;

    private Collider _playerCollider;
    private Rigidbody _playerRigidbody;
    private VehiclePickupAnimator _vehicleAnimator;
    private bool _delivered;

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
        if (_delivered || !_vehicleAnimator.HasPackage())
            return;

        _delivered = true;
        _vehicleAnimator.StartDeliverySequence(OnPushComplete);
        _playerCollider = null;
        _playerRigidbody = null;
        _vehicleAnimator = null;
    }

    void OnPushComplete(Transform package)
    {
        ParticleSystem smoke = package.GetComponentInChildren<ParticleSystem>();
        StartCoroutine(DeliverPackage(package, smoke));
    }

    IEnumerator DeliverPackage(Transform package, ParticleSystem smoke)
    {
        if (smoke != null)
        {
            smoke.transform.SetParent(null);
            smoke.Play();
        }

        yield return new WaitForSeconds(0.4f);
        package.gameObject.SetActive(false);

        if (smoke != null)
        {
            yield return new WaitUntil(() => !smoke.IsAlive(true));
            Destroy(smoke.gameObject);
        }

        gameObject.SetActive(false);
    }
}
