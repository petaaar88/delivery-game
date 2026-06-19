using System.Collections.Generic;
using UnityEngine;

public class ArrowNavigator : MonoBehaviour
{
    [Tooltip("Root GameObject of the player vehicle.")]
    public GameObject vehicle;

    [Tooltip("Height above the vehicle root in world space.")]
    public float heightAboveVehicle = 4f;

    [Tooltip("Arrow rotation speed in degrees/sec. 0 = instant snap.")]
    public float rotationSpeed = 270f;

    [Tooltip("Degrees added to the computed Y angle. Adjust until the arrow tip points forward.")]
    public float modelForwardAngleOffset = 0f;

    [Tooltip("How often (seconds) to recompute the nav path from the vehicle's current position.")]
    public float pathRefreshInterval = 1f;

    private Renderer[] _renderers;
    private Transform _destination;
    private List<Vector3> _path = new List<Vector3>();
    private bool _active;
    private float _refreshTimer;

    void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>();
        SetVisible(false);
        DeliveryManager.OnDeliveryStarted   += HandleDeliveryStarted;
        DeliveryManager.OnDeliveryCompleted += HandleDeliveryCompleted;
    }

    void OnDestroy()
    {
        DeliveryManager.OnDeliveryStarted   -= HandleDeliveryStarted;
        DeliveryManager.OnDeliveryCompleted -= HandleDeliveryCompleted;
    }

    void HandleDeliveryStarted(Transform destination)
    {
        _destination  = destination;
        _active       = true;
        RefreshPath();
        _refreshTimer = pathRefreshInterval;
        SetVisible(true);
    }

    void HandleDeliveryCompleted()
    {
        _active      = false;
        _destination = null;
        _path.Clear();
        if (NavigationGraph.Instance != null) NavigationGraph.Instance.ClearPath();
        SetVisible(false);
    }

    void LateUpdate()
    {
        if (!_active || vehicle == null || vehicle.transform == null) return;

        // Keep arrow floating above vehicle in world space.
        transform.position = vehicle.transform.position + Vector3.up * heightAboveVehicle;

        // Periodic path refresh from current vehicle position.
        _refreshTimer -= Time.deltaTime;
        if (_refreshTimer <= 0f)
        {
            RefreshPath();
            _refreshTimer = pathRefreshInterval;
        }

        // _path[0] = node nearest to vehicle (already "at" it), aim for _path[1].
        Vector3 target;
        if (_path.Count >= 2)
            target = _path[1];
        else if (_path.Count == 1)
            target = _path[0];
        else
            target = _destination != null ? _destination.position : transform.position;

        // Rotate on Y only — arrow stays flat regardless of terrain tilt.
        Vector3 dir = new Vector3(target.x - vehicle.transform.position.x, 0f, target.z - vehicle.transform.position.z);
        if (dir.sqrMagnitude > 0.01f)
        {
            float targetAngle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + modelForwardAngleOffset;
            float newAngle = rotationSpeed > 0f
                ? Mathf.MoveTowardsAngle(transform.eulerAngles.y, targetAngle, rotationSpeed * Time.deltaTime)
                : targetAngle;
            transform.rotation = Quaternion.Euler(0f, newAngle, 0f);
        }
    }

    void RefreshPath()
    {
        _path.Clear();
        if (NavigationGraph.Instance == null)
        {
            Debug.LogWarning("[Arrow] NavigationGraph.Instance is null");
        }
        else if (vehicle != null && vehicle.transform != null && _destination != null)
        {
            _path = NavigationGraph.Instance.FindPath(vehicle.transform.position, _destination.position);
            Debug.Log($"[Arrow] Path refreshed: {_path.Count} node(s). Vehicle={vehicle.transform.position} Dest={_destination.position}");
            for (int i = 0; i < _path.Count; i++)
                Debug.Log($"[Arrow]   [{i}] {_path[i]}");
        }

        if (_destination != null)
            _path.Add(_destination.position);
    }

    void SetVisible(bool visible)
    {
        foreach (var r in _renderers) r.enabled = visible;
    }
}
