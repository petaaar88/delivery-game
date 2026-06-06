using UnityEngine;
using UnityEngine.InputSystem;

public class PackagePickup : MonoBehaviour
{
    public float speedThreshold = 0.5f;

    private Transform _packageVisual;
    private GameObject _triggerZone;
    private bool _pickedUp;
    private Vector3 _originalLocalPos;
    private Quaternion _originalLocalRot;

    private Collider _playerCollider;
    private Rigidbody _playerRigidbody;

    void Awake()
    {
        _packageVisual = transform.Find("Package");
        _originalLocalPos = _packageVisual.localPosition;
        _originalLocalRot = _packageVisual.localRotation;
        _triggerZone = transform.Find("TriggerZone").gameObject;
    }

    void Update()
    {
        if (Keyboard.current.rKey.wasPressedThisFrame)
            ResetPackage();

        if (_pickedUp || _playerCollider == null)
            return;

        if (_playerRigidbody.linearVelocity.magnitude <= speedThreshold)
            TriggerPickup();
    }

    void OnTriggerEnter(Collider other)
    {
        if (_pickedUp || !other.CompareTag("Player"))
            return;

        _playerCollider = other;
        _playerRigidbody = other.GetComponentInParent<Rigidbody>();
    }

    void OnTriggerExit(Collider other)
    {
        if (other == _playerCollider)
        {
            _playerCollider = null;
            _playerRigidbody = null;
        }
    }

    void TriggerPickup()
    {
        VehiclePickupAnimator animator = _playerCollider.GetComponentInParent<VehiclePickupAnimator>();
        if (animator == null)
            return;

        _pickedUp = true;
        GetComponent<Collider>().enabled = false;
        _triggerZone.SetActive(false);
        animator.StartPickupSequence(_packageVisual);
    }

    public void ResetPackage()
    {
        _packageVisual.SetParent(transform);
        _packageVisual.localPosition = _originalLocalPos;
        _packageVisual.localRotation = _originalLocalRot;
        _packageVisual.gameObject.SetActive(true);
        _triggerZone.SetActive(true);
        GetComponent<Collider>().enabled = true;
        _playerCollider = null;
        _playerRigidbody = null;
        _pickedUp = false;
    }
}
