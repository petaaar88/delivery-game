using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Binds the gameplay HUD (coins, deliveries, timer, speed, message banner)
/// to GameSession and DeliveryManager events.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class HudController : MonoBehaviour
{
    const float LowTimeThreshold = 10f;

    // Speedometer tuning.
    const float SpeedSmoothTime = 0.14f;   // seconds; higher = calmer needle/number
    const float GaugeMaxSpeed = 160f;      // km/h at which the needle is fully swept
    const float NeedleMinAngle = -120f;    // needle angle at 0 km/h
    const float NeedleMaxAngle = 120f;     // needle angle at GaugeMaxSpeed
    const float FastEnterSpeed = 115f;     // km/h that triggers the speed pop
    const float FastExitSpeed = 100f;      // km/h to drop out of it (hysteresis)
    const int DeliveriesGoal = 10;         // deliveries per run (shown as X / goal)

    // Gauge ring colour ramp: calm green -> gold -> hot red.
    static readonly Color GaugeGreen = new Color(0.474f, 0.847f, 0.435f);
    static readonly Color GaugeGold = new Color(1f, 0.773f, 0.239f);
    static readonly Color GaugeRed = new Color(0.910f, 0.333f, 0.357f);

    // Bright, candy-coloured confetti pieces.
    static readonly Color[] ConfettiColors =
    {
        new Color(1f, 0.5f, 0.39f),        // coral
        new Color(1f, 0.773f, 0.239f),     // gold
        new Color(0.474f, 0.847f, 0.435f), // green
        new Color(0.451f, 0.808f, 1f),     // sky
        new Color(0.541f, 0.451f, 0.941f), // purple
        new Color(1f, 0.85f, 0.4f),        // yellow
    };

    // Hot exhaust-style sparks for the speedo burst.
    static readonly Color[] SpeedSparkColors =
    {
        new Color(1f, 0.96f, 0.84f),       // white-hot
        new Color(1f, 0.82f, 0.30f),       // gold
        new Color(1f, 0.60f, 0.24f),       // orange
        new Color(0.91f, 0.33f, 0.36f),    // red
    };

    // Golden glitter for the coin counter.
    static readonly Color[] CoinSparkColors =
    {
        new Color(1f, 0.96f, 0.78f),
        new Color(1f, 0.82f, 0.30f),
        new Color(1f, 0.72f, 0.16f),
    };

    Label _coinsLabel;
    Label _deliveriesLabel;
    Label _timerLabel;
    Label _speedLabel;
    Label _banner;
    Label _rewardToast;
    VisualElement _root;
    VisualElement _timerPill;
    VisualElement _needle;
    VisualElement _gauge;
    VisualElement _speedPanel;
    VisualElement _coinsPill;
    VisualElement _coinBadge;
    VisualElement _deliveryCard;
    VisualElement _routeFill;
    VisualElement _timerFill;

    float _displaySpeed;
    float _speedVelocity;
    bool _fast;

    IVisualElementScheduledItem _bannerHide;
    IVisualElementScheduledItem _toastHide;

    void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        _root = root;

        _coinsLabel = root.Q<Label>("coins-label");
        _deliveriesLabel = root.Q<Label>("deliveries-label");
        _timerLabel = root.Q<Label>("timer-label");
        _speedLabel = root.Q<Label>("speed-label");
        _banner = root.Q<Label>("banner");
        _rewardToast = root.Q<Label>("reward-toast");
        _timerPill = root.Q("timer-pill");
        _needle = root.Q("speed-needle");
        _gauge = root.Q("speed-gauge");
        _speedPanel = root.Q("speed-panel");
        _coinsPill = root.Q("coins-pill");
        _coinBadge = _coinsPill?.Q(className: "coin-badge");
        _deliveryCard = root.Q("delivery-card");
        _routeFill = root.Q("route-fill");
        _timerFill = root.Q("timer-fill");

        // Clear the static placeholder widths baked into the stylesheet.
        if (_routeFill != null)
            _routeFill.style.width = Length.Percent(0f);
        if (_timerFill != null)
            _timerFill.style.width = Length.Percent(0f);
        if (_deliveriesLabel != null)
            _deliveriesLabel.text = $"0/{DeliveriesGoal}";

        GameSession.OnCoinsChanged += HandleCoinsChanged;
        GameSession.OnDeliveriesChanged += HandleDeliveriesChanged;
        GameSession.OnDeliveryRewarded += HandleDeliveryRewarded;
        DeliveryManager.OnDeliveryStarted += HandleDeliveryStarted;
        DeliveryManager.OnDeliveryZoneEntered += HandleZoneEntered;
        DeliveryManager.OnDeliveryZoneExited += HandleZoneExited;
        DeliveryManager.OnDeliveryTriggered += HandleDeliveryTriggered;
        DeliveryManager.OnDeliveryCompleted += HandleDeliveryCompleted;
    }

    void OnDisable()
    {
        GameSession.OnCoinsChanged -= HandleCoinsChanged;
        GameSession.OnDeliveriesChanged -= HandleDeliveriesChanged;
        GameSession.OnDeliveryRewarded -= HandleDeliveryRewarded;
        DeliveryManager.OnDeliveryStarted -= HandleDeliveryStarted;
        DeliveryManager.OnDeliveryZoneEntered -= HandleZoneEntered;
        DeliveryManager.OnDeliveryZoneExited -= HandleZoneExited;
        DeliveryManager.OnDeliveryTriggered -= HandleDeliveryTriggered;
        DeliveryManager.OnDeliveryCompleted -= HandleDeliveryCompleted;
    }

    void Start()
    {
        ShowBanner("PICK UP A PACKAGE!", 3f);
    }

    void Update()
    {
        UpdateSpeed();
        UpdateTimer();
    }

    void UpdateSpeed()
    {
        var sceneManager = RCC_SceneManager.Instance;
        var vehicle = sceneManager != null ? sceneManager.activePlayerVehicle : null;

        // Use the forward component of velocity rather than RCC's full
        // velocity magnitude: it ignores suspension bounce and sideways skid,
        // so the reading is both steadier and closer to a real speedo value.
        float target = 0f;
        if (vehicle != null)
        {
            Vector3 velocity = vehicle.Rigid.linearVelocity;
            target = Mathf.Abs(Vector3.Dot(velocity, vehicle.transform.forward)) * 3.6f;
        }

        _displaySpeed = Mathf.SmoothDamp(_displaySpeed, target, ref _speedVelocity,
            SpeedSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);

        _speedLabel.text = Mathf.RoundToInt(_displaySpeed).ToString();

        float fill = Mathf.Clamp01(_displaySpeed / GaugeMaxSpeed);

        if (_needle != null)
        {
            float angle = Mathf.Lerp(NeedleMinAngle, NeedleMaxAngle, fill);
            _needle.style.rotate = new StyleRotate(new Rotate(new Angle(angle, AngleUnit.Degree)));
        }

        if (_gauge != null)
        {
            Color ring = SpeedZoneColor(_displaySpeed);
            _gauge.style.borderTopColor = ring;
            _gauge.style.borderRightColor = ring;
            _gauge.style.borderBottomColor = ring;
            _gauge.style.borderLeftColor = ring;
        }

        if (_speedPanel != null)
        {
            if (!_fast && _displaySpeed >= FastEnterSpeed)
            {
                _fast = true;
                _speedPanel.AddToClassList("speed--fast");
                SpawnSparks(_gauge, 14, 110f, SpeedSparkColors);
                PopPanel(_speedPanel);
            }
            else if (_fast && _displaySpeed <= FastExitSpeed)
            {
                _fast = false;
                _speedPanel.RemoveFromClassList("speed--fast");
            }
        }
    }

    // Green when cruising, gold mid-range, red near top speed.
    static Color SpeedZoneColor(float kmh)
    {
        if (kmh <= 60f)
            return Color.Lerp(GaugeGreen, GaugeGold, Mathf.Clamp01(kmh / 60f));

        return Color.Lerp(GaugeGold, GaugeRed, Mathf.Clamp01((kmh - 60f) / 60f));
    }

    /// <summary>
    /// Fires a quick radial confetti burst from a point given in percent of the
    /// screen. Pieces are throwaway VisualElements that fly out, spin, fade via
    /// a USS transition, then remove themselves — no pooling needed for bursts.
    /// </summary>
    void SpawnConfetti(float xPercent, float yPercent, int count, float spread)
    {
        if (_root == null)
            return;

        for (int i = 0; i < count; i++)
        {
            var piece = new VisualElement { pickingMode = PickingMode.Ignore };
            piece.AddToClassList("confetti");

            float w = Random.Range(9f, 16f);
            piece.style.width = w;
            piece.style.height = w * Random.Range(0.5f, 1f);
            piece.style.backgroundColor = ConfettiColors[Random.Range(0, ConfettiColors.Length)];
            piece.style.left = Length.Percent(xPercent);
            piece.style.top = Length.Percent(yPercent);

            float startRot = Random.Range(0f, 360f);
            piece.style.rotate = new StyleRotate(new Rotate(new Angle(startRot, AngleUnit.Degree)));
            _root.Add(piece);

            float angle = Random.Range(0f, Mathf.PI * 2f);
            float dist = Random.Range(spread * 0.35f, spread);
            float dx = Mathf.Cos(angle) * dist;
            float dy = Mathf.Sin(angle) * dist + spread * 0.28f; // gravity drift
            float endRot = startRot + Random.Range(-320f, 320f);

            var p = piece;
            p.schedule.Execute(() =>
            {
                p.style.translate = new StyleTranslate(new Translate(dx, dy));
                p.style.rotate = new StyleRotate(new Rotate(new Angle(endRot, AngleUnit.Degree)));
                p.style.opacity = 0f;
            }).ExecuteLater(16);

            p.schedule.Execute(() => p.RemoveFromHierarchy()).ExecuteLater(1050);
        }
    }

    /// <summary>
    /// Short elastic scale bounce on a panel (class-driven, see .panel-pop).
    /// </summary>
    static void PopPanel(VisualElement panel)
    {
        if (panel == null)
            return;

        panel.AddToClassList("panel-pop");
        panel.schedule.Execute(() => panel.RemoveFromClassList("panel-pop")).ExecuteLater(200);
    }

    /// <summary>
    /// Radial spark burst that visibly originates from the given element:
    /// pieces are parented to it and fly out from its centre. Each spark is an
    /// elongated streak rotated to match its flight direction.
    /// </summary>
    static void SpawnSparks(VisualElement origin, int count, float distance, Color[] palette)
    {
        if (origin == null)
            return;

        for (int i = 0; i < count; i++)
        {
            var spark = new VisualElement { pickingMode = PickingMode.Ignore };
            spark.AddToClassList("spark");

            float length = Random.Range(14f, 26f);
            float thickness = Random.Range(4f, 6f);
            spark.style.width = length;
            spark.style.height = thickness;
            spark.style.marginLeft = -length * 0.5f;
            spark.style.marginTop = -thickness * 0.5f;
            spark.style.backgroundColor = palette[Random.Range(0, palette.Length)];

            // Streak points along its flight path.
            float angleDeg = Random.Range(0f, 360f);
            spark.style.rotate = new StyleRotate(new Rotate(new Angle(angleDeg, AngleUnit.Degree)));
            origin.Add(spark);

            float rad = angleDeg * Mathf.Deg2Rad;
            float dist = Random.Range(distance * 0.55f, distance);
            float dx = Mathf.Cos(rad) * dist;
            float dy = Mathf.Sin(rad) * dist;

            var s = spark;
            s.schedule.Execute(() =>
            {
                s.style.translate = new StyleTranslate(new Translate(dx, dy));
                s.style.opacity = 0f;
                s.style.scale = new StyleScale(new Scale(new Vector2(0.3f, 0.3f)));
            }).ExecuteLater(16);

            s.schedule.Execute(() => s.RemoveFromHierarchy()).ExecuteLater(650);
        }
    }

    void UpdateTimer()
    {
        var session = GameSession.Instance;
        if (session == null || session.TimeRemaining < 0f)
            return;

        float t = session.TimeRemaining;
        _timerLabel.text = string.Format("{0}:{1:00}", (int)(t / 60f), (int)(t % 60f));

        bool low = t <= LowTimeThreshold && t > 0f;
        _timerPill.EnableInClassList("timer--low", low);

        // Heartbeat pulse on the countdown while time is running out.
        float pulse = low ? 1f + 0.1f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f)) : 1f;
        _timerLabel.style.scale = new StyleScale(new Scale(new Vector2(pulse, pulse)));

        if (_timerFill != null)
        {
            float fraction = session.TimeAllotted > 0f
                ? Mathf.Clamp01(session.TimeRemaining / session.TimeAllotted)
                : 0f;
            _timerFill.style.width = Length.Percent(fraction * 100f);
        }
    }

    void HandleCoinsChanged(int coins)
    {
        _coinsLabel.text = coins.ToString();
        PopPanel(_coinsPill);
        SpawnSparks(_coinBadge, 8, 52f, CoinSparkColors);
    }

    void HandleDeliveriesChanged(int deliveries)
    {
        _deliveriesLabel.text = $"{deliveries}/{DeliveriesGoal}";

        if (_routeFill != null)
        {
            float fraction = Mathf.Clamp01(deliveries / (float)DeliveriesGoal);
            _routeFill.style.width = Length.Percent(fraction * 100f);
        }

        // Big celebration the moment the run goal is reached.
        if (deliveries == DeliveriesGoal)
            SpawnConfetti(50f, 40f, 40, 440f);
    }

    void HandleDeliveryStarted(Transform destination)
    {
        _timerPill.AddToClassList("timer--visible");
        PopPanel(_deliveryCard);
        ShowBanner("DELIVER THE PACKAGE!", 2.5f);
    }

    void HandleZoneEntered()
    {
        ShowBanner("STOP TO DELIVER!", -1f);
    }

    void HandleZoneExited()
    {
        HideBanner();
    }

    void HandleDeliveryTriggered()
    {
        HideBanner();
        _timerPill.RemoveFromClassList("timer--visible");
    }

    void HandleDeliveryRewarded(int baseReward, int bonus)
    {
        int total = baseReward + bonus;
        _rewardToast.text = bonus > 0 ? $"+{total}  (fast bonus +{bonus})" : $"+{total}";
        _rewardToast.AddToClassList("toast--show");

        // Confetti on every delivery, with an extra burst for a fast arrival.
        SpawnConfetti(50f, 34f, 18, 320f);
        if (bonus > 0)
            SpawnConfetti(50f, 34f, 22, 400f);

        _toastHide?.Pause();
        _toastHide = _rewardToast.schedule.Execute(() => _rewardToast.RemoveFromClassList("toast--show"));
        _toastHide.ExecuteLater(2200);
    }

    void HandleDeliveryCompleted()
    {
        _timerPill.RemoveFromClassList("timer--visible");
        ShowBanner("PICK UP A PACKAGE!", 2.5f);
    }

    void ShowBanner(string text, float durationSeconds)
    {
        _banner.text = text;
        _banner.AddToClassList("banner--show");

        _bannerHide?.Pause();
        if (durationSeconds > 0f)
        {
            _bannerHide = _banner.schedule.Execute(HideBanner);
            _bannerHide.ExecuteLater((long)(durationSeconds * 1000f));
        }
    }

    void HideBanner()
    {
        _bannerHide?.Pause();
        _banner.RemoveFromClassList("banner--show");
    }
}
