using System.Collections;
using UnityEngine;

public class PackageDelivery : MonoBehaviour
{
    public float speedThreshold = 0.5f;
    public string packageDisappearSound = "PackageDisappear";

    private Collider _playerCollider;
    private Rigidbody _playerRigidbody;
    private VehiclePickupAnimator _vehicleAnimator;
    private ObjectAudioManager _audio;
    private bool _delivered;
    private GameObject _triggerZone;

    void Awake()
    {
        _triggerZone = transform.Find("TriggerZone").gameObject;
    }

    public void Activate()
    {
        _delivered = false;
        GetComponent<Collider>().enabled = true;
        _triggerZone.SetActive(true);
    }

    public void Deactivate()
    {
        GetComponent<Collider>().enabled = false;
        _triggerZone.SetActive(false);
    }

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
        DeliveryManager.Instance?.NotifyDeliveryZoneEntered();
    }

    void OnTriggerExit(Collider other)
    {
        if (other == _playerCollider)
        {
            _playerCollider = null;
            _playerRigidbody = null;
            _vehicleAnimator = null;
            if (!_delivered)
                DeliveryManager.Instance?.NotifyDeliveryZoneExited();
        }
    }

    void TriggerDelivery()
    {
        if (_delivered || !_vehicleAnimator.HasPackage())
            return;

        _delivered = true;
        Deactivate();
        DeliveryManager.Instance?.NotifyDeliveryTriggered();
        _audio = _vehicleAnimator.audioManager;
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

        yield return new WaitForSeconds(0.35f);

        // Brief bulge, then shrink away — beats blinking out of existence.
        if (_audio != null)
            _audio.PlaySoundOneShot(packageDisappearSound);

        Vector3 baseScale = package.localScale;
        float duration = 0.3f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float bulge = 1f + 0.2f * Mathf.Sin(t * Mathf.PI);
            float shrink = 1f - t * t * t;
            package.localScale = baseScale * (bulge * shrink);
            yield return null;
        }
        package.gameObject.SetActive(false);
        package.localScale = baseScale;

        if (smoke != null)
        {
            yield return new WaitUntil(() => !smoke.IsAlive(true));
            Destroy(smoke.gameObject);
        }

        Deactivate();
        if (DeliveryManager.Instance != null)
            DeliveryManager.Instance.OnPackageDelivered();
    }
}
