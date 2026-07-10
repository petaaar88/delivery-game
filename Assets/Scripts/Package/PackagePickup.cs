using UnityEngine;
using UnityEngine.Rendering.Universal;

public class PackagePickup : MonoBehaviour
{
    public PackageVariant[] variants;
    public Transform packageSpawnPosition;
    public float speedThreshold = 0.5f;

    private GameObject _spawnedPackage;
    private bool _pickedUp;

    private Collider _playerCollider;
    private Rigidbody _playerRigidbody;
    private GameObject _triggerZone;

    void Awake()
    {
        _triggerZone = transform.Find("TriggerZone").gameObject;
        ConfigureIndicatorRenderers(_triggerZone);
    }

    void Start()
    {
        EnableIndicatorBloom();
    }

    void ConfigureIndicatorRenderers(GameObject root)
    {
        // Pickup indicators are navigational UI in the world: keep them
        // readable without any incoming or outgoing scene shadows.
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    void EnableIndicatorBloom()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
            return;

        UniversalAdditionalCameraData cameraData = mainCamera.GetComponent<UniversalAdditionalCameraData>();
        if (cameraData != null)
            cameraData.renderPostProcessing = true;
    }

    void Update()
    {
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
        if (animator == null || variants == null || variants.Length == 0)
            return;

        _pickedUp = true;
        _playerCollider = null;
        _playerRigidbody = null;

        // Spawn the package only now, on pickup — idle locations no longer each hold one in memory.
        PackageVariant variant = variants[Random.Range(0, variants.Length)];
        _spawnedPackage = Instantiate(variant.prefab, packageSpawnPosition.position, packageSpawnPosition.rotation);

        IPackageEffect effect = variant.effect as IPackageEffect;
        animator.StartPickupSequence(_spawnedPackage.transform, effect);

        DeliveryManager.Instance?.OnPackagePickedUp(this);
    }

    public void Activate()
    {
        if (_pickedUp) return;
        GetComponent<Collider>().enabled = true;
        _triggerZone.SetActive(true);
    }

    public void Deactivate()
    {
        GetComponent<Collider>().enabled = false;
        _triggerZone.SetActive(false);
    }

    public void ResetPackage()
    {
        if (_spawnedPackage != null)
        {
            Destroy(_spawnedPackage);
            _spawnedPackage = null;
        }

        _playerCollider = null;
        _playerRigidbody = null;
        _pickedUp = false;

        GetComponent<Collider>().enabled = true;
        _triggerZone.SetActive(true);
    }
}
