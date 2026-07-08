using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// UI Toolkit touch controls (steer arrows, gas/brake pedals, handbrake) that
/// feed RCC through the static RCC_MobileButtons.mobileInputs struct — the same
/// path RCC_InputManager already reads when mobileControllerEnabled is on.
/// The RCC uGUI canvas stays disabled; this is the only writer of those inputs.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class MobileControlsController : MonoBehaviour
{
    public enum Mode { Auto, ForceOn, ForceOff }

    [Tooltip("Auto shows controls on mobile platforms only. ForceOn is useful for testing in the editor.")]
    public Mode mode = Mode.Auto;

    [Header("Steering feel")]
    [Tooltip("How fast steer input ramps toward the pressed direction (units/sec).")]
    public float steerSpeed = 4f;
    [Tooltip("How fast steer input returns to center when released (units/sec).")]
    public float steerGravity = 6f;

    bool _active;
    bool _steerLeft;
    bool _steerRight;
    bool _gas;
    bool _brake;
    bool _handbrake;
    float _steer;
    bool _prevMobileControllerEnabled;

    void OnEnable()
    {
        _active = mode == Mode.ForceOn || (mode == Mode.Auto && Application.isMobilePlatform);

        var root = GetComponent<UIDocument>().rootVisualElement;

        if (!_active)
            return;

        root.Q("hud-root").AddToClassList("is-mobile");

        // RCC_InputManager only reads mobileInputs when this global flag is on.
        if (RCC_Settings.Instance != null)
        {
            _prevMobileControllerEnabled = RCC_Settings.Instance.mobileControllerEnabled;
            RCC_Settings.Instance.mobileControllerEnabled = true;
        }

        BindHold(root.Q("steer-left"), pressed => _steerLeft = pressed);
        BindHold(root.Q("steer-right"), pressed => _steerRight = pressed);
        BindHold(root.Q("pedal-gas"), pressed => _gas = pressed);
        BindHold(root.Q("pedal-brake"), pressed => _brake = pressed);
        BindHold(root.Q("handbrake-btn"), pressed => _handbrake = pressed);
    }

    void OnDisable()
    {
        if (!_active)
            return;

        WriteInputs(0f, 0f, 0f, 0f);

#if UNITY_EDITOR
        // The RCC_Settings asset lives across editor play sessions; a leftover
        // "true" makes RCC's platform-check dialog block the next play start.
        if (RCC_Settings.Instance != null)
            RCC_Settings.Instance.mobileControllerEnabled = _prevMobileControllerEnabled;
#endif
    }

    void Update()
    {
        if (!_active)
            return;

        float target = (_steerLeft ? -1f : 0f) + (_steerRight ? 1f : 0f);
        float rate = Mathf.Approximately(target, 0f) ? steerGravity : steerSpeed;
        _steer = Mathf.MoveTowards(_steer, target, rate * Time.unscaledDeltaTime);

        WriteInputs(_gas ? 1f : 0f, _brake ? 1f : 0f, _steer, _handbrake ? 1f : 0f);
    }

    static void WriteInputs(float throttle, float brake, float steer, float handbrake)
    {
        var inputs = RCC_MobileButtons.mobileInputs;
        if (inputs == null)
            return;

        inputs.throttleInput = throttle;
        inputs.brakeInput = brake;
        inputs.steerInput = steer;
        inputs.handbrakeInput = handbrake;
        inputs.boostInput = 0f;
    }

    static void BindHold(VisualElement element, System.Action<bool> setPressed)
    {
        if (element == null)
            return;

        element.RegisterCallback<PointerDownEvent>(evt =>
        {
            element.CapturePointer(evt.pointerId);
            setPressed(true);
            evt.StopPropagation();
        });
        element.RegisterCallback<PointerUpEvent>(evt =>
        {
            element.ReleasePointer(evt.pointerId);
            setPressed(false);
            evt.StopPropagation();
        });
        element.RegisterCallback<PointerCancelEvent>(evt =>
        {
            element.ReleasePointer(evt.pointerId);
            setPressed(false);
        });
    }
}
