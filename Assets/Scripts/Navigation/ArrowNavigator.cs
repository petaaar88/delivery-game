using System.Collections;
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

    [Header("Pop Animation")]
    public float popInDuration  = 0.4f;
    public float popInOvershoot = 1.28f;
    public float popOutDuration = 0.22f;
    public float popOutBulge    = 0.18f;

    private Renderer[] _renderers;
    private Transform _destination;
    private List<Vector3> _path = new List<Vector3>();
    private bool _active;
    private float _refreshTimer;
    private Coroutine _popCoroutine;
    private Vector3 _baseScale;

    void Awake()
    {
        _baseScale = transform.localScale;
        _renderers = GetComponentsInChildren<Renderer>();
        transform.localScale = Vector3.zero;
        foreach (var r in _renderers) r.enabled = false;

        DeliveryManager.OnDeliveryStarted      += HandleDeliveryStarted;
        DeliveryManager.OnDeliveryZoneEntered  += HideArrow;
        DeliveryManager.OnDeliveryZoneExited   += HandleDeliveryZoneExited;
        DeliveryManager.OnDeliveryTriggered    += HandleDeliveryCompleted;
        DeliveryManager.OnDeliveryCompleted    += HandleDeliveryCompleted;
        VehiclePickupAnimator.OnPickupComplete += HandlePickupComplete;
    }

    void OnDestroy()
    {
        DeliveryManager.OnDeliveryStarted      -= HandleDeliveryStarted;
        DeliveryManager.OnDeliveryZoneEntered  -= HideArrow;
        DeliveryManager.OnDeliveryZoneExited   -= HandleDeliveryZoneExited;
        DeliveryManager.OnDeliveryTriggered    -= HandleDeliveryCompleted;
        DeliveryManager.OnDeliveryCompleted    -= HandleDeliveryCompleted;
        VehiclePickupAnimator.OnPickupComplete -= HandlePickupComplete;
    }

    void HandleDeliveryStarted(Transform destination)
    {
        _destination = destination;
    }

    void HandlePickupComplete()
    {
        if (_destination == null) return;
        _active       = true;
        RefreshPath();
        _refreshTimer = pathRefreshInterval;
        ShowArrow();
    }

    void HandleDeliveryCompleted()
    {
        _active      = false;
        _destination = null;
        _path.Clear();
        if (NavigationGraph.Instance != null) NavigationGraph.Instance.ClearPath();
        HideArrow();
    }

    void HandleDeliveryZoneExited()
    {
        if (_active) ShowArrow();
    }

    void ShowArrow()
    {
        if (_popCoroutine != null) StopCoroutine(_popCoroutine);
        _popCoroutine = StartCoroutine(PopIn());
    }

    void HideArrow()
    {
        if (_popCoroutine != null) StopCoroutine(_popCoroutine);
        _popCoroutine = StartCoroutine(PopOut());
    }

    IEnumerator PopIn()
    {
        foreach (var r in _renderers) r.enabled = true;
        transform.localScale = Vector3.zero;

        // Phase 1: EaseOutCubic scale from 0 up to the overshoot peak.
        float phase1  = popInDuration * 0.6f;
        float elapsed = 0f;
        while (elapsed < phase1)
        {
            elapsed += Time.deltaTime;
            float t       = Mathf.Clamp01(elapsed / phase1);
            float easeOut = 1f - (1f - t) * (1f - t) * (1f - t);
            transform.localScale = _baseScale * Mathf.Lerp(0f, popInOvershoot, easeOut);
            yield return null;
        }

        // Phase 2: damped cosine oscillation from overshoot back to 1.0 — matches LandingSettle pattern.
        float phase2 = popInDuration * 0.4f;
        elapsed = 0f;
        while (elapsed < phase2)
        {
            elapsed += Time.deltaTime;
            float t      = Mathf.Clamp01(elapsed / phase2);
            float damp   = (1f - t) * (1f - t);
            float offset = (popInOvershoot - 1f) * Mathf.Cos(t * Mathf.PI * 2.5f) * damp;
            transform.localScale = _baseScale * (1f + offset);
            yield return null;
        }

        transform.localScale = _baseScale;
        _popCoroutine = null;
    }

    IEnumerator PopOut()
    {
        // Quick bulge then cubic shrink to zero — matches PackageDelivery disappear pattern.
        float elapsed = 0f;
        while (elapsed < popOutDuration)
        {
            elapsed += Time.deltaTime;
            float t      = Mathf.Clamp01(elapsed / popOutDuration);
            float bulge  = 1f + popOutBulge * Mathf.Sin(t * Mathf.PI * 0.5f);
            float shrink = 1f - t * t * t;
            transform.localScale = _baseScale * (bulge * shrink);
            yield return null;
        }

        transform.localScale = Vector3.zero;
        foreach (var r in _renderers) r.enabled = false;
        _popCoroutine = null;
    }

    void LateUpdate()
    {
        if (!_active || vehicle == null || vehicle.transform == null) return;

        transform.position = vehicle.transform.position + Vector3.up * heightAboveVehicle;

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
}
