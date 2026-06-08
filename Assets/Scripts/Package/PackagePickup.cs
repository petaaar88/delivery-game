using UnityEngine;
using UnityEngine.InputSystem;

public class PackagePickup : MonoBehaviour
{
    public PackageVariant[] variants;
    public Transform packageSpawnPosition;
    public float speedThreshold = 0.5f;

    private Transform _packageVisual;
    private GameObject _spawnedPackage;
    private PackageVariant _activeVariant;
    private GameObject _triggerZone;
    private bool _pickedUp;

    private Collider _playerCollider;
    private Rigidbody _playerRigidbody;

    void Awake()
    {
        _triggerZone = transform.Find("TriggerZone").gameObject;
        SpawnVariant();
    }

    void SpawnVariant()
    {
        if (variants == null || variants.Length == 0) return;
        _activeVariant = variants[Random.Range(0, variants.Length)];
        _spawnedPackage = Instantiate(_activeVariant.prefab, packageSpawnPosition.position, packageSpawnPosition.rotation);
        _packageVisual = _spawnedPackage.transform;
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

        IPackageEffect effect = _activeVariant?.effect as IPackageEffect;
        animator.StartPickupSequence(_packageVisual, effect);
    }

    public void ResetPackage()
    {
        if (_spawnedPackage != null)
            Destroy(_spawnedPackage);

        _triggerZone.SetActive(true);
        GetComponent<Collider>().enabled = true;
        _playerCollider = null;
        _playerRigidbody = null;
        _pickedUp = false;

        SpawnVariant();
    }
}
