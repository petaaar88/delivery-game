using UnityEngine;

/// <summary>
/// Tracks run progress: coins earned, deliveries completed and the global run
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
    public static event System.Action OnTimeExpired;

    [Header("Rewards")]
    public int baseReward = 50;
    public int maxTimeBonus = 50;
    [Tooltip("Coins lost when the package is destroyed (clamped so coins never go negative).")]
    public int failPenalty = 25;

    [Header("Run timer")]
    [Tooltip("Total seconds for the whole delivery run.")]
    public float globalRunTime = 300f;

    public int Coins { get; private set; }
    public int DeliveriesCompleted { get; private set; }
    public bool HasPackage { get; private set; }
    /// <summary>Seconds left for the whole run.</summary>
    public float TimeRemaining { get; private set; } = -1f;
    public float TimeAllotted { get; private set; }

    private bool _timerRunning;
    private bool _timeExpired;

    void Awake()
    {
        Instance = this;
        TimeAllotted = Mathf.Max(0f, globalRunTime);
        TimeRemaining = TimeAllotted;
        _timerRunning = TimeAllotted > 0f;
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
        {
            TimeRemaining = Mathf.Max(0f, TimeRemaining - Time.deltaTime);
            if (TimeRemaining <= 0f)
                ExpireTimer();
        }

        if (!_timeExpired && TimeRemaining <= 0f)
            ExpireTimer();
    }

    void ExpireTimer()
    {
        _timerRunning = false;
        _timeExpired = true;
        OnTimeExpired?.Invoke();
    }

    void HandleDeliveryStarted(Transform destination)
    {
        HasPackage = true;
    }

    void HandleDeliveryTriggered()
    {
        if (!HasPackage)
            return;

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
    }

    void HandleDeliveryFailed()
    {
        HasPackage = false;

        int penalty = Mathf.Min(Coins, failPenalty);
        Coins -= penalty;
        OnDeliveryFailedPenalty?.Invoke(penalty);
        OnCoinsChanged?.Invoke(Coins);
    }
}
