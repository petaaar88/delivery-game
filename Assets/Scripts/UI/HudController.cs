using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Binds the gameplay HUD (coins, deliveries, timer, speed, message banner)
/// to GameSession and DeliveryManager events.
/// </summary>
[RequireComponent(typeof(UIDocument))]
[RequireComponent(typeof(ObjectAudioManager))]
public class HudController : MonoBehaviour
{
    const float LowTimeThreshold = 10f;
    const string PickupBannerText = "PICK UP A PACKAGE!";
    const string DeliverBannerText = "DELIVER THE PACKAGE!";
    const string ExplodedBannerText = "PACKAGE EXPLODED!";
    const float PickupBannerAspect = 2170f / 725f;
    const float DeliverBannerAspect = 2172f / 724f;
    const float ExplodedBannerAspect = 2172f / 724f;
    const float ImageBannerMaxWidth = 1092f;
    const float ImageBannerWidthRatio = 0.952f;

    // Speedometer tuning.
    const float SpeedSmoothTime = 0.14f;   // seconds; higher = calmer number
    const float GaugeMaxSpeed = 160f;      // km/h treated as "top speed" for colour + shake
    const float ShakeStartSpeed = 90f;     // km/h where the number starts to vibrate
    const float FastEnterSpeed = 115f;     // km/h that triggers the speed pop
    const float FastExitSpeed = 100f;      // km/h to drop out of it (hysteresis)
    const int DeliveriesGoal = 10;         // deliveries per run (shown as X / goal)

    // Speed number colour ramp (tuned to read on the light pill): green -> amber -> red.
    static readonly Color SpeedSlow = new Color(0.235f, 0.647f, 0.294f);
    static readonly Color SpeedMid = new Color(0.910f, 0.573f, 0.078f);
    static readonly Color SpeedFast = new Color(0.878f, 0.227f, 0.188f);

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
    Label _timeUpLabel;
    VisualElement _root;
    VisualElement _activeBanner;
    VisualElement _pickupBanner;
    VisualElement _pickupBannerImage;
    VisualElement _deliverBanner;
    VisualElement _deliverBannerImage;
    VisualElement _explodedBanner;
    VisualElement _explodedBannerImage;
    VisualElement _timerPill;
    VisualElement _speedPanel;
    VisualElement _coinsPill;
    VisualElement _coinBadge;
    VisualElement _routeFill;
    VisualElement _timerFill;
    ObjectAudioManager _audioManager;

    float _displaySpeed;
    float _speedVelocity;
    bool _fast;

    IVisualElementScheduledItem _bannerHide;
    IVisualElementScheduledItem _bannerPunch;
    IVisualElementScheduledItem _bannerSettle;
    IVisualElementScheduledItem _bannerKick;
    IVisualElementScheduledItem _bannerRest;
    IVisualElementScheduledItem _toastHide;
    IVisualElementScheduledItem _timeUpEnter;
    IVisualElementScheduledItem _timeUpSettle;

    void OnEnable()
    {
        _audioManager = GetComponent<ObjectAudioManager>();

        var root = GetComponent<UIDocument>().rootVisualElement;
        _root = root;

        _coinsLabel = root.Q<Label>("coins-label");
        _deliveriesLabel = root.Q<Label>("deliveries-label");
        _timerLabel = root.Q<Label>("timer-label");
        _speedLabel = root.Q<Label>("speed-label");
        _banner = root.Q<Label>("banner");
        _pickupBanner = root.Q("pickup-banner");
        _pickupBannerImage = root.Q("pickup-banner-image");
        _deliverBanner = root.Q("deliver-banner");
        _deliverBannerImage = root.Q("deliver-banner-image");
        _explodedBanner = root.Q("exploded-banner");
        _explodedBannerImage = root.Q("exploded-banner-image");
        _rewardToast = root.Q<Label>("reward-toast");
        _timeUpLabel = root.Q<Label>("time-up-label");
        _timerPill = root.Q("timer-pill");
        _speedPanel = root.Q("speed-panel");
        _coinsPill = root.Q("coins-pill");
        _coinBadge = _coinsPill?.Q(className: "coin-badge");
        _routeFill = root.Q("route-fill");
        _timerFill = root.Q("timer-fill");

        // Clear the static placeholder widths baked into the stylesheet.
        if (_routeFill != null)
            _routeFill.style.width = Length.Percent(0f);
        if (_timerFill != null)
            _timerFill.style.width = Length.Percent(0f);
        _timerPill?.AddToClassList("timer--visible");
        if (_deliveriesLabel != null)
            _deliveriesLabel.text = $"0/{DeliveriesGoal}";

        GameSession.OnCoinsChanged += HandleCoinsChanged;
        GameSession.OnDeliveriesChanged += HandleDeliveriesChanged;
        GameSession.OnDeliveryRewarded += HandleDeliveryRewarded;
        GameSession.OnDeliveryFailedPenalty += HandleDeliveryFailedPenalty;
        GameSession.OnTimeExpired += HandleTimeExpired;
        DeliveryManager.OnDeliveryStarted += HandleDeliveryStarted;
        DeliveryManager.OnDeliveryZoneEntered += HandleZoneEntered;
        DeliveryManager.OnDeliveryZoneExited += HandleZoneExited;
        DeliveryManager.OnDeliveryTriggered += HandleDeliveryTriggered;
        DeliveryManager.OnDeliveryCompleted += HandleDeliveryCompleted;
        DeliveryManager.OnDeliveryFailed += HandleDeliveryFailed;
    }

    void OnDisable()
    {
        GameSession.OnCoinsChanged -= HandleCoinsChanged;
        GameSession.OnDeliveriesChanged -= HandleDeliveriesChanged;
        GameSession.OnDeliveryRewarded -= HandleDeliveryRewarded;
        GameSession.OnDeliveryFailedPenalty -= HandleDeliveryFailedPenalty;
        GameSession.OnTimeExpired -= HandleTimeExpired;
        DeliveryManager.OnDeliveryStarted -= HandleDeliveryStarted;
        DeliveryManager.OnDeliveryZoneEntered -= HandleZoneEntered;
        DeliveryManager.OnDeliveryZoneExited -= HandleZoneExited;
        DeliveryManager.OnDeliveryTriggered -= HandleDeliveryTriggered;
        DeliveryManager.OnDeliveryCompleted -= HandleDeliveryCompleted;
        DeliveryManager.OnDeliveryFailed -= HandleDeliveryFailed;

        PauseBannerAnimation();
        PauseTimeUpAnimation();
    }

    void Start()
    {
        ShowBanner(PickupBannerText, 4f);
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

        if (_speedLabel != null)
        {
            _speedLabel.text = Mathf.RoundToInt(_displaySpeed).ToString();
            _speedLabel.style.color = SpeedTextColor(_displaySpeed);
        }

        if (_speedPanel != null)
        {
            // The number vibrates around Z (with a little jitter) as it nears top
            // speed — 0 below ShakeStartSpeed, full at GaugeMaxSpeed.
            float shake = Mathf.Clamp01(Mathf.InverseLerp(ShakeStartSpeed, GaugeMaxSpeed, _displaySpeed));
            if (shake > 0f)
            {
                float t = Time.unscaledTime;
                float rot = Mathf.Sin(t * 46f) * 6f * shake;
                float dx = Mathf.Sin(t * 63f) * 3.5f * shake;
                float dy = Mathf.Cos(t * 57f) * 3.5f * shake;
                _speedPanel.style.rotate = new StyleRotate(new Rotate(new Angle(rot, AngleUnit.Degree)));
                _speedPanel.style.translate = new StyleTranslate(new Translate(dx, dy));
            }
            else
            {
                _speedPanel.style.rotate = new StyleRotate(new Rotate(new Angle(0f, AngleUnit.Degree)));
                _speedPanel.style.translate = new StyleTranslate(new Translate(0f, 0f));
            }

            // Crossing into "fast": red-hot frame, a spark burst and a pop (hysteresis out).
            if (!_fast && _displaySpeed >= FastEnterSpeed)
            {
                _fast = true;
                _speedPanel.AddToClassList("speed--fast");
                SpawnSparks(_speedLabel, 14, 96f, SpeedSparkColors);
                PopPanel(_speedPanel);
            }
            else if (_fast && _displaySpeed <= FastExitSpeed)
            {
                _fast = false;
                _speedPanel.RemoveFromClassList("speed--fast");
            }
        }
    }

    // Solid green while cruising, warming through amber, red near top speed.
    static Color SpeedTextColor(float kmh)
    {
        if (kmh <= 45f)
            return SpeedSlow;
        if (kmh <= 100f)
            return Color.Lerp(SpeedSlow, SpeedMid, (kmh - 45f) / 55f);

        return Color.Lerp(SpeedMid, SpeedFast, Mathf.Clamp01((kmh - 100f) / 60f));
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
        if (_timerLabel == null || session == null || session.TimeAllotted <= 0f)
            return;

        float t = Mathf.Max(0f, session.TimeRemaining);
        _timerLabel.text = string.Format("{0:00}:{1:00}", (int)(t / 60f), (int)(t % 60f));

        bool low = t <= LowTimeThreshold;
        _timerPill?.EnableInClassList("timer--low", low);

        // Heartbeat pulse on the WHOLE pill (not just the digits, which would
        // spill out) while time is running out.
        if (_timerPill != null)
        {
            float pulse = low && t > 0f ? 1f + 0.055f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f)) : 1f;
            _timerPill.style.scale = new StyleScale(new Scale(new Vector2(pulse, pulse)));
        }

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
        ShowBanner(DeliverBannerText, 3.5f);
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
    }

    void HandleDeliveryRewarded(int baseReward, int bonus)
    {
        int total = baseReward + bonus;
        _rewardToast.text = bonus > 0 ? $"+{total}  (fast bonus +{bonus})" : $"+{total}";
        _rewardToast.AddToClassList("toast--show");
        _audioManager?.PlaySoundOneShot("DeliverySuccessful");

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
        ShowBanner(PickupBannerText, 4f);
    }

    void HandleDeliveryFailed()
    {
        FlashScreen(new Color(1f, 0.25f, 0.2f), 0.4f, 600);
        ShowBanner(ExplodedBannerText, 3.5f);
    }

    void HandleDeliveryFailedPenalty(int penalty)
    {
        if (penalty <= 0)
            return;

        _rewardToast.text = $"-{penalty}";
        _rewardToast.AddToClassList("toast--show");

        _toastHide?.Pause();
        _toastHide = _rewardToast.schedule.Execute(() => _rewardToast.RemoveFromClassList("toast--show"));
        _toastHide.ExecuteLater(2200);
    }

    void HandleTimeExpired()
    {
        HideBanner();
        FlashScreen(new Color(1f, 0.86f, 0.32f), 0.22f, 420);
        ShowTimeUp();
    }

    void ShowTimeUp()
    {
        if (_timeUpLabel == null)
            return;

        PauseTimeUpAnimation();

        _timeUpLabel.style.opacity = 0f;
        _timeUpLabel.style.translate = new StyleTranslate(new Translate(0f, 86f));
        _timeUpLabel.style.scale = new StyleScale(new Scale(new Vector2(0.35f, 0.35f)));
        _timeUpLabel.style.rotate = new StyleRotate(new Rotate(new Angle(-6f, AngleUnit.Degree)));

        _timeUpEnter = _timeUpLabel.schedule.Execute(() =>
        {
            _timeUpLabel.style.opacity = 1f;
            _timeUpLabel.style.translate = new StyleTranslate(new Translate(0f, 0f));
            _timeUpLabel.style.scale = new StyleScale(new Scale(new Vector2(1.18f, 1.18f)));
            _timeUpLabel.style.rotate = new StyleRotate(new Rotate(new Angle(1f, AngleUnit.Degree)));
        });
        _timeUpEnter.ExecuteLater(16);

        _timeUpSettle = _timeUpLabel.schedule.Execute(() =>
        {
            _timeUpLabel.style.scale = new StyleScale(new Scale(new Vector2(1f, 1f)));
            _timeUpLabel.style.rotate = new StyleRotate(new Rotate(new Angle(0f, AngleUnit.Degree)));
        });
        _timeUpSettle.ExecuteLater(260);
    }

    void PauseTimeUpAnimation()
    {
        _timeUpEnter?.Pause();
        _timeUpSettle?.Pause();
    }

    /// <summary>
    /// Full-screen colour flash that fades out via an inline USS transition —
    /// a throwaway VisualElement like the confetti, no stylesheet class needed.
    /// </summary>
    void FlashScreen(Color color, float peakOpacity, int fadeMs)
    {
        if (_root == null)
            return;

        var flash = new VisualElement { pickingMode = PickingMode.Ignore };
        flash.style.position = Position.Absolute;
        flash.style.left = 0f;
        flash.style.right = 0f;
        flash.style.top = 0f;
        flash.style.bottom = 0f;
        flash.style.backgroundColor = color;
        flash.style.opacity = peakOpacity;
        flash.style.transitionProperty = new List<StylePropertyName> { new StylePropertyName("opacity") };
        flash.style.transitionDuration = new List<TimeValue> { new TimeValue(fadeMs, TimeUnit.Millisecond) };
        _root.Add(flash);

        flash.schedule.Execute(() => flash.style.opacity = 0f).ExecuteLater(30);
        flash.schedule.Execute(() => flash.RemoveFromHierarchy()).ExecuteLater(fadeMs + 120);
    }

    void ShowBanner(string text, float durationSeconds)
    {
        PauseBannerAnimation();
        _bannerHide?.Pause();

        bool useImageBanner = TryGetImageBanner(text, out var imageBanner, out var imageElement, out float imageAspect);

        if (text == PickupBannerText)
            _audioManager?.PlaySoundOneShot("PickupPackage");
        else if (text == DeliverBannerText)
            _audioManager?.PlaySoundOneShot("DeliverPackage");
        else if (text == ExplodedBannerText)
            _audioManager?.PlaySoundOneShot("PackageExploded");

        HideBannerElement(_banner);
        HideBannerElement(_pickupBanner);
        HideBannerElement(_deliverBanner);
        HideBannerElement(_explodedBanner);

        _activeBanner = useImageBanner ? imageBanner : _banner;
        if (_activeBanner == null)
            return;

        if (!useImageBanner && _banner != null)
            _banner.text = text;
        if (useImageBanner)
            LayoutImageBanner(imageBanner, imageElement, imageAspect);

        PrepareBannerElement(_activeBanner);
        _activeBanner.AddToClassList("banner--show");

        var target = _activeBanner;
        _bannerPunch = target.schedule.Execute(() =>
        {
            if (_activeBanner != target)
                return;

            target.style.opacity = 1f;
            target.style.translate = new StyleTranslate(new Translate(0f, 0f));
            target.style.scale = new StyleScale(new Scale(new Vector2(1.42f, 1.42f)));
            target.style.rotate = new StyleRotate(new Rotate(new Angle(7f, AngleUnit.Degree)));
        });
        _bannerPunch.ExecuteLater(12);

        _bannerSettle = target.schedule.Execute(() =>
        {
            if (_activeBanner != target)
                return;

            target.style.translate = new StyleTranslate(new Translate(-16f, 0f));
            target.style.scale = new StyleScale(new Scale(new Vector2(0.82f, 0.82f)));
            target.style.rotate = new StyleRotate(new Rotate(new Angle(-5f, AngleUnit.Degree)));
        });
        _bannerSettle.ExecuteLater(125);

        _bannerKick = target.schedule.Execute(() =>
        {
            if (_activeBanner != target)
                return;

            target.style.translate = new StyleTranslate(new Translate(12f, 0f));
            target.style.scale = new StyleScale(new Scale(new Vector2(1.15f, 1.15f)));
            target.style.rotate = new StyleRotate(new Rotate(new Angle(3f, AngleUnit.Degree)));
        });
        _bannerKick.ExecuteLater(235);

        _bannerRest = target.schedule.Execute(() =>
        {
            if (_activeBanner != target)
                return;

            target.style.translate = new StyleTranslate(new Translate(0f, 0f));
            target.style.scale = new StyleScale(new Scale(new Vector2(1f, 1f)));
            target.style.rotate = new StyleRotate(new Rotate(new Angle(0f, AngleUnit.Degree)));
        });
        _bannerRest.ExecuteLater(380);

        if (durationSeconds > 0f)
        {
            _bannerHide = target.schedule.Execute(HideBanner);
            _bannerHide.ExecuteLater((long)(durationSeconds * 1000f));
        }
    }

    void HideBanner()
    {
        _bannerHide?.Pause();
        PauseBannerAnimation();
        HideBannerElement(_activeBanner);
        HideBannerElement(_banner);
        HideBannerElement(_pickupBanner);
        HideBannerElement(_deliverBanner);
        HideBannerElement(_explodedBanner);
        _activeBanner = null;
    }

    static void PrepareBannerElement(VisualElement banner)
    {
        if (banner == null)
            return;

        banner.style.opacity = 0f;
        banner.style.translate = new StyleTranslate(new Translate(0f, 92f));
        banner.style.scale = new StyleScale(new Scale(new Vector2(0.18f, 0.18f)));
        banner.style.rotate = new StyleRotate(new Rotate(new Angle(-10f, AngleUnit.Degree)));
    }

    static void HideBannerElement(VisualElement banner)
    {
        if (banner == null)
            return;

        banner.RemoveFromClassList("banner--show");
        banner.style.opacity = 0f;
        banner.style.translate = new StyleTranslate(new Translate(0f, -34f));
        banner.style.scale = new StyleScale(new Scale(new Vector2(0.78f, 0.78f)));
        banner.style.rotate = new StyleRotate(new Rotate(new Angle(0f, AngleUnit.Degree)));
    }

    bool TryGetImageBanner(string text, out VisualElement banner, out VisualElement image, out float aspect)
    {
        if (text == PickupBannerText && _pickupBanner != null)
        {
            banner = _pickupBanner;
            image = _pickupBannerImage ?? _pickupBanner;
            aspect = PickupBannerAspect;
            return true;
        }

        if (text == DeliverBannerText && _deliverBanner != null)
        {
            banner = _deliverBanner;
            image = _deliverBannerImage ?? _deliverBanner;
            aspect = DeliverBannerAspect;
            return true;
        }

        if (text == ExplodedBannerText && _explodedBanner != null)
        {
            banner = _explodedBanner;
            image = _explodedBannerImage ?? _explodedBanner;
            aspect = ExplodedBannerAspect;
            return true;
        }

        banner = null;
        image = null;
        aspect = PickupBannerAspect;
        return false;
    }

    void LayoutImageBanner(VisualElement banner, VisualElement image, float aspect)
    {
        if (banner == null)
            return;

        float rootWidth = _root?.resolvedStyle.width ?? 0f;
        float rootHeight = _root?.resolvedStyle.height ?? 0f;
        float screenWidth = Screen.width;
        if (float.IsNaN(rootWidth) || rootWidth <= 0f)
            rootWidth = screenWidth;
        else if (screenWidth > 0f && rootWidth < screenWidth * 0.85f)
            rootWidth = screenWidth;
        if (float.IsNaN(rootHeight) || rootHeight <= 0f)
            rootHeight = Screen.height;

        float targetWidth = Mathf.Min(rootWidth * ImageBannerWidthRatio, ImageBannerMaxWidth);
        targetWidth = Mathf.Max(0f, Mathf.Min(targetWidth, rootWidth - 28f));
        float targetHeight = targetWidth / Mathf.Max(0.01f, aspect);
        float top = Mathf.Clamp(rootHeight * 0.16f, 110f, 148f);

        banner.style.left = 0f;
        banner.style.right = 0f;
        banner.style.height = targetHeight;
        banner.style.top = top;

        var content = image ?? banner;
        content.style.width = targetWidth;
        content.style.height = targetHeight;
    }

    void PauseBannerAnimation()
    {
        _bannerPunch?.Pause();
        _bannerSettle?.Pause();
        _bannerKick?.Pause();
        _bannerRest?.Pause();
    }
}
