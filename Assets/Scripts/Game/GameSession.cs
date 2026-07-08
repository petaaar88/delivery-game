using UnityEngine;

/// <summary>
/// Tracks run progress: coins earned, deliveries completed and the per-delivery
/// countdown. Rewards are granted on DeliveryManager.OnDeliveryTriggered (real
/// deliveries only — debug resets fire OnDeliveryCompleted without a trigger).
/// </summary>
public class GameSession : MonoBehaviour
{
    public static GameSession Instance { get; private set; }

    public static event System.Action<int> OnCoinsChanged;
    public static event System.Action<int> OnDeliveriesChanged;
    /// <summary>baseReward, timeBonus — fired when a delivery is rewarded.</summary>
    public static event System.Action<int, int> OnDeliveryRewarded;
    /// <summary>Coins actually deducted — fired when a delivery fails (package destroyed).</summary>
    public static event System.Action<int> OnDeliveryFailedPenalty;

    [Header("Rewards")]
    public int baseReward = 50;
    public int maxTimeBonus = 50;
    [Tooltip("Coins lost when the package is destroyed (clamped so coins never go negative).")]
    public int failPenalty = 25;

    [Header("Delivery timer")]
    [Tooltip("Flat seconds granted per delivery.")]
    public float baseDeliveryTime = 20f;
    [Tooltip("Extra seconds per meter of straight-line distance to the destination.")]
    public float timePerMeter = 0.12f;

    public int Coins { get; private set; }
    public int DeliveriesCompleted { get; private set; }
    public bool HasPackage { get; private set; }
    /// <summary>Seconds left for the current delivery; negative when no delivery is active.</summary>
    public float TimeRemaining { get; private set; } = -1f;
    public float TimeAllotted { get; private set; }

    private bool _timerRunning;

    void Awake()
    {
        Instance = this;
    }

    void OnEnable()
    {
        DeliveryManager.OnDeliveryStarted += HandleDeliveryStarted;
        DeliveryManager.OnDeliveryTriggered += HandleDeliveryTriggered;
        DeliveryManager.OnDeliveryCompleted += HandleDeliveryCompleted;
        DeliveryManager.OnDeliveryFailed += HandleDeliveryFailed;
    }

    void OnDisable()
    {
        DeliveryManager.OnDeliveryStarted -= HandleDeliveryStarted;
        DeliveryManager.OnDeliveryTriggered -= HandleDeliveryTriggered;
        DeliveryManager.OnDeliveryCompleted -= HandleDeliveryCompleted;
        DeliveryManager.OnDeliveryFailed -= HandleDeliveryFailed;
    }

    void Update()
    {
        if (_timerRunning && TimeRemaining > 0f)
            TimeRemaining = Mathf.Max(0f, TimeRemaining - Time.deltaTime);
    }

    void HandleDeliveryStarted(Transform destination)
    {
        HasPackage = true;

        float distance = 250f;
        var vehicle = RCC_SceneManager.Instance != null ? RCC_SceneManager.Instance.activePlayerVehicle : null;
        if (vehicle != null)
            distance = Vector3.Distance(vehicle.transform.position, destination.position);

        TimeAllotted = baseDeliveryTime + distance * timePerMeter;
        TimeRemaining = TimeAllotted;
        _timerRunning = true;
    }

    void HandleDeliveryTriggered()
    {
        if (!HasPackage)
            return;

        _timerRunning = false;

        int bonus = 0;
        if (TimeRemaining > 0f && TimeAllotted > 0f)
            bonus = Mathf.RoundToInt(maxTimeBonus * (TimeRemaining / TimeAllotted));

        Coins += baseReward + bonus;
        DeliveriesCompleted++;

        OnDeliveryRewarded?.Invoke(baseReward, bonus);
        OnCoinsChanged?.Invoke(Coins);
        OnDeliveriesChanged?.Invoke(DeliveriesCompleted);
    }

    void HandleDeliveryCompleted()
    {
        HasPackage = false;
        _timerRunning = false;
        TimeRemaining = -1f;
    }

    void HandleDeliveryFailed()
    {
        HasPackage = false;
        _timerRunning = false;
        TimeRemaining = -1f;

        int penalty = Mathf.Min(Coins, failPenalty);
        Coins -= penalty;
        OnDeliveryFailedPenalty?.Invoke(penalty);
        OnCoinsChanged?.Invoke(Coins);
    }
}
