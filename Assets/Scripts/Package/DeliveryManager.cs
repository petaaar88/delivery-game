using UnityEngine;
using UnityEngine.InputSystem;

public class DeliveryManager : MonoBehaviour
{
    public static DeliveryManager Instance { get; private set; }

    private PackagePickup[] _pickups;
    private PackageDelivery[] _destinations;
    private PackagePickup _lastUsedPickup;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        _pickups = FindObjectsByType<PackagePickup>(FindObjectsSortMode.None);
        _destinations = FindObjectsByType<PackageDelivery>(FindObjectsSortMode.None);

        foreach (var p in _pickups) p.Activate();
        foreach (var d in _destinations) d.Deactivate();
    }

    void Update()
    {
        if (Keyboard.current.rKey.wasPressedThisFrame)
            DebugResetAll();
    }

    public void OnPackagePickedUp(PackagePickup source)
    {
        _lastUsedPickup = source;
        foreach (var p in _pickups) p.Deactivate();

        if (_destinations.Length == 0) return;
        _destinations[Random.Range(0, _destinations.Length)].Activate();
    }

    public void OnPackageDelivered()
    {
        if (_lastUsedPickup != null)
            _lastUsedPickup.ResetPackage();

        foreach (var p in _pickups)
        {
            if (p != _lastUsedPickup) p.Activate();
        }
        _lastUsedPickup = null;
    }

    void DebugResetAll()
    {
        foreach (var d in _destinations) d.Deactivate();
        foreach (var p in _pickups) p.ResetPackage();
        _lastUsedPickup = null;
    }
}
