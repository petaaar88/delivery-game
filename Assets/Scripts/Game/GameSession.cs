using System;
using System.Collections.Generic;
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
    /// <summary>baseReward, coinTimeBonus, secondsAdded.</summary>
    public static event Action<int, int, int> OnDeliveryRewarded;
    /// <summary>Coins deducted, seconds deducted when an explosive package detonates.</summary>
    public static event Action<int, int> OnDeliveryFailedPenalty;
    public static event Action<float> OnRushStarted;
    /// <summary>True when the rush package reached its destination before its timer expired.</summary>
    public static event Action<bool> OnRushEnded;
    public static event Action OnTimeExpired;

    [Header("Rewards")]
    public int baseReward = 50;
    public int maxTimeBonus = 50;
    [Tooltip("Coins lost when the package is destroyed (clamped so coins never go negative).")]
    public int failPenalty = 25;

    [Header("Time adjustments")]
    [Tooltip("Seconds added to the global run timer after every successful delivery.")]
    public int successfulDeliveryTimeBonus = 20;
    [Tooltip("Seconds removed from the global run timer when an explosive package detonates.")]
    public int explosivePackageTimePenalty = 10;

    [Header("Run timer")]
    [Tooltip("Total seconds for the whole delivery run.")]
    public float globalRunTime = 90f;

    public int Coins { get; private set; }
    public int DeliveriesCompleted { get; private set; }
    public bool HasPackage { get; private set; }
    public bool IsGameOver { get; private set; }
    /// <summary>Seconds left for the whole run.</summary>
    public float TimeRemaining { get; private set; } = -1f;
    public float TimeAllotted { get; private set; }
    public bool IsRushActive { get; private set; }
    public float RushTimeRemaining { get; private set; }
    public float RushTimeAllotted { get; private set; }
    public int RushAttempts { get; private set; }
    public int RushDeliveriesCompleted { get; private set; }
    public int Explosions { get; private set; }
    public int DeliveryTimeEarned { get; private set; }
    public int ExplosionTimeLost { get; private set; }
    public int FinalLeaderboardRank { get; private set; }

    private bool _timerRunning;
    private bool _timeExpired;

    void Awake()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
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
        if (IsGameOver)
            return;

        if (_timerRunning && TimeRemaining > 0f)
        {
            TimeRemaining = Mathf.Max(0f, TimeRemaining - Time.deltaTime);
            if (TimeRemaining <= 0f)
            {
                ExpireTimer();
                return;
            }
        }

        if (!_timeExpired && TimeRemaining <= 0f)
        {
            ExpireTimer();
            return;
        }

        if (IsRushActive)
        {
            RushTimeRemaining = Mathf.Max(0f, RushTimeRemaining - Time.deltaTime);
            if (RushTimeRemaining <= 0f)
                EndRushPackage(false);
        }
    }

    void ExpireTimer()
    {
        if (_timeExpired)
            return;

        _timerRunning = false;
        _timeExpired = true;
        IsGameOver = true;
        if (IsRushActive)
            EndRushPackage(false);

        FinalLeaderboardRank = LocalLeaderboard.AddResult(new LocalLeaderboardEntry
        {
            score = Coins,
            deliveries = DeliveriesCompleted,
            rushDeliveries = RushDeliveriesCompleted,
            rushAttempts = RushAttempts,
            explosions = Explosions,
            timeEarned = DeliveryTimeEarned,
            timeLost = ExplosionTimeLost,
        });

        OnTimeExpired?.Invoke();
        Time.timeScale = 0f;
    }

    void HandleDeliveryStarted(Transform destination)
    {
        if (IsGameOver)
            return;

        HasPackage = true;
    }

    void HandleDeliveryTriggered()
    {
        if (!HasPackage || IsGameOver)
            return;

        int bonus = 0;
        if (TimeRemaining > 0f && TimeAllotted > 0f)
            bonus = Mathf.RoundToInt(maxTimeBonus * Mathf.Clamp01(TimeRemaining / TimeAllotted));

        Coins += baseReward + bonus;
        DeliveriesCompleted++;

        bool rushDeliveredInTime = IsRushActive && RushTimeRemaining > 0f;
        if (rushDeliveredInTime)
            RushDeliveriesCompleted++;

        int secondsAdded = Mathf.Max(0, successfulDeliveryTimeBonus);
        if (secondsAdded > 0)
        {
            TimeRemaining += secondsAdded;
            DeliveryTimeEarned += secondsAdded;
        }

        OnDeliveryRewarded?.Invoke(baseReward, bonus, secondsAdded);
        OnCoinsChanged?.Invoke(Coins);
        OnDeliveriesChanged?.Invoke(DeliveriesCompleted);

        if (rushDeliveredInTime)
            EndRushPackage(true);
    }

    void HandleDeliveryCompleted()
    {
        HasPackage = false;
    }

    void HandleDeliveryFailed()
    {
        if (IsGameOver)
            return;

        HasPackage = false;
        Explosions++;

        int penalty = Mathf.Min(Coins, failPenalty);
        Coins -= penalty;

        int timePenalty = Mathf.Max(0, explosivePackageTimePenalty);
        if (timePenalty > 0)
        {
            TimeRemaining = Mathf.Max(0f, TimeRemaining - timePenalty);
            ExplosionTimeLost += timePenalty;
        }

        OnDeliveryFailedPenalty?.Invoke(penalty, timePenalty);
        OnCoinsChanged?.Invoke(Coins);

        if (TimeRemaining <= 0f)
            ExpireTimer();
    }

    public void BeginRushPackage(float duration)
    {
        if (IsGameOver)
            return;

        if (IsRushActive)
            EndRushPackage(false);

        RushTimeAllotted = Mathf.Max(1f, duration);
        RushTimeRemaining = RushTimeAllotted;
        IsRushActive = true;
        RushAttempts++;
        OnRushStarted?.Invoke(RushTimeAllotted);
    }

    public void EndRushPackage(bool deliveredInTime)
    {
        if (!IsRushActive)
            return;

        IsRushActive = false;
        RushTimeRemaining = 0f;
        OnRushEnded?.Invoke(deliveredInTime);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (IsGameOver)
            Time.timeScale = 1f;
    }
}

[Serializable]
public class LocalLeaderboardEntry
{
    public string id;
    public string completedAt;
    public int score;
    public int deliveries;
    public int rushDeliveries;
    public int rushAttempts;
    public int explosions;
    public int timeEarned;
    public int timeLost;
}

[Serializable]
class LocalLeaderboardData
{
    public List<LocalLeaderboardEntry> entries = new List<LocalLeaderboardEntry>();
}

/// <summary>Small device-local leaderboard backed by PlayerPrefs.</summary>
public static class LocalLeaderboard
{
    const string PlayerPrefsKey = "DeliveryGame.LocalLeaderboard.v1";
    public const int MaxEntries = 10;

    public static int AddResult(LocalLeaderboardEntry entry)
    {
        if (entry == null)
            return 0;

        entry.id = Guid.NewGuid().ToString("N");
        entry.completedAt = DateTime.UtcNow.ToString("o");

        LocalLeaderboardData data = Load();
        data.entries.Add(entry);
        Sort(data.entries);

        int rank = data.entries.FindIndex(candidate => candidate.id == entry.id) + 1;
        if (data.entries.Count > MaxEntries)
            data.entries.RemoveRange(MaxEntries, data.entries.Count - MaxEntries);

        Save(data);
        return rank <= MaxEntries ? rank : 0;
    }

    public static List<LocalLeaderboardEntry> GetEntries()
    {
        LocalLeaderboardData data = Load();
        Sort(data.entries);
        return new List<LocalLeaderboardEntry>(data.entries);
    }

    static LocalLeaderboardData Load()
    {
        string json = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
        if (string.IsNullOrEmpty(json))
            return new LocalLeaderboardData();

        try
        {
            LocalLeaderboardData data = JsonUtility.FromJson<LocalLeaderboardData>(json);
            if (data == null || data.entries == null)
                return new LocalLeaderboardData();
            return data;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Could not load local leaderboard: {exception.Message}");
            return new LocalLeaderboardData();
        }
    }

    static void Save(LocalLeaderboardData data)
    {
        PlayerPrefs.SetString(PlayerPrefsKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    static void Sort(List<LocalLeaderboardEntry> entries)
    {
        entries.Sort((left, right) =>
        {
            int comparison = right.score.CompareTo(left.score);
            if (comparison != 0) return comparison;

            comparison = right.deliveries.CompareTo(left.deliveries);
            if (comparison != 0) return comparison;

            comparison = right.rushDeliveries.CompareTo(left.rushDeliveries);
            if (comparison != 0) return comparison;

            comparison = left.explosions.CompareTo(right.explosions);
            if (comparison != 0) return comparison;

            return string.CompareOrdinal(right.completedAt, left.completedAt);
        });
    }
}
