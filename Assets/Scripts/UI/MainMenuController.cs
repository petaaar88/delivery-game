using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
[RequireComponent(typeof(ObjectAudioManager))]
public class MainMenuController : MonoBehaviour
{
    [Tooltip("Scene loaded by the PLAY button.")]
    public string gameScene = "GameScene";

    [Header("Scene showcase")]
    [Tooltip("Name of the preloaded van object already present in the MainMenu scene.")]
    public string parkedVanName = "Menu Van";
    [Tooltip("Camera used for the menu showcase. Defaults to Camera.main.")]
    public Camera menuCamera;

    [Header("Showcase pose")]
    public bool applyVanPose = false;
    public Vector3 vanPosition = new Vector3(41.5f, 1.09f, 4.0f);
    public Vector3 vanEulerAngles = new Vector3(0f, -72.64f, 0f);
    public bool applyCameraPose = false;
    public Vector3 cameraPosition = new Vector3(31.8f, 1.35f, 7.6f);
    public Vector3 cameraTargetOffset = new Vector3(-4.0f, 1.15f, -2.2f);
    public float cameraFieldOfView = 42f;

    [Header("Idle motion")]
    public float idleBobAmount = 0.035f;
    public float idleYawAmount = 0.9f;
    public float idleRollAmount = 0.25f;

    Button _playButton;
    Button _quitButton;
    Button _settingsButton;
    Button _settingsBackButton;
    VisualElement _settingsOverlay;
    ObjectAudioManager _audioManager;
    Slider _masterSlider;
    Slider _sfxSlider;
    Slider _musicSlider;
    Slider _vehicleSlider;
    Transform _parkedVan;
    Vector3 _vanBasePosition;
    Quaternion _vanBaseRotation;
    Transform[] _wheels;
    Vector3[] _wheelBasePositions;
    Quaternion[] _wheelBaseRotations;

    void OnEnable()
    {
        _audioManager = GetComponent<ObjectAudioManager>();

        var root = GetComponent<UIDocument>().rootVisualElement;

        _playButton = root.Q<Button>("play-button");
        _quitButton = root.Q<Button>("quit-button");
        _settingsButton = root.Q<Button>("settings-preview-button");
        _settingsBackButton = root.Q<Button>("settings-back-button");
        _settingsOverlay = root.Q("menu-settings-overlay");
        _masterSlider = root.Q<Slider>("master-slider");
        _sfxSlider = root.Q<Slider>("sfx-slider");
        _musicSlider = root.Q<Slider>("music-slider");
        _vehicleSlider = root.Q<Slider>("vehicle-slider");

        if (_playButton != null)
        {
            _playButton.clicked += Play;
            _playButton.clicked += PlayClickSound;
        }

        if (_quitButton != null)
        {
            _quitButton.clicked += Quit;
            _quitButton.clicked += PlayClickSound;
        }

        if (_settingsButton != null)
        {
            _settingsButton.clicked += ShowSettings;
            _settingsButton.clicked += PlayClickSound;
        }

        if (_settingsBackButton != null)
        {
            _settingsBackButton.clicked += HideSettings;
            _settingsBackButton.clicked += PlayClickSound;
        }

        if (_masterSlider != null)
            _masterSlider.RegisterValueChangedCallback(OnMasterVolumeChanged);

        if (_sfxSlider != null)
            _sfxSlider.RegisterValueChangedCallback(OnSfxVolumeChanged);

        if (_musicSlider != null)
            _musicSlider.RegisterValueChangedCallback(OnMusicVolumeChanged);

        if (_vehicleSlider != null)
            _vehicleSlider.RegisterValueChangedCallback(OnVehicleVolumeChanged);

        HideSettings();
    }

    void OnDisable()
    {
        if (_playButton != null)
        {
            _playButton.clicked -= Play;
            _playButton.clicked -= PlayClickSound;
        }

        if (_quitButton != null)
        {
            _quitButton.clicked -= Quit;
            _quitButton.clicked -= PlayClickSound;
        }

        if (_settingsButton != null)
        {
            _settingsButton.clicked -= ShowSettings;
            _settingsButton.clicked -= PlayClickSound;
        }

        if (_settingsBackButton != null)
        {
            _settingsBackButton.clicked -= HideSettings;
            _settingsBackButton.clicked -= PlayClickSound;
        }

        if (_masterSlider != null)
            _masterSlider.UnregisterValueChangedCallback(OnMasterVolumeChanged);

        if (_sfxSlider != null)
            _sfxSlider.UnregisterValueChangedCallback(OnSfxVolumeChanged);

        if (_musicSlider != null)
            _musicSlider.UnregisterValueChangedCallback(OnMusicVolumeChanged);

        if (_vehicleSlider != null)
            _vehicleSlider.UnregisterValueChangedCallback(OnVehicleVolumeChanged);
    }

    void Start()
    {
        ConfigureSceneShowcase();
    }

    void Update()
    {
        if (_parkedVan == null)
            return;

        float t = Time.unscaledTime;
        _parkedVan.position = _vanBasePosition + new Vector3(0f, Mathf.Sin(t * 1.6f) * idleBobAmount, 0f);
        _parkedVan.rotation = _vanBaseRotation * Quaternion.Euler(Mathf.Sin(t * 1.4f) * idleRollAmount, Mathf.Sin(t * 0.9f) * idleYawAmount, Mathf.Sin(t * 1.7f) * idleRollAmount);

        // The wheels are children of the van root, so the idle bob/sway above would drag
        // them along. Pin each wheel model back to its rest pose in world space so they stay
        // planted on the asphalt while only the body appears to move on its suspension.
        if (_wheels != null)
        {
            for (int i = 0; i < _wheels.Length; i++)
            {
                if (_wheels[i] != null)
                    _wheels[i].SetPositionAndRotation(_wheelBasePositions[i], _wheelBaseRotations[i]);
            }
        }
    }

    void PlayClickSound()
    {
        _audioManager?.PlaySoundOneShot("ButtonClick");
    }

    void Play()
    {
        SceneManager.LoadScene(gameScene);
    }

    void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void ShowSettings()
    {
        SyncSliders();
        if (_settingsOverlay != null)
            _settingsOverlay.AddToClassList("overlay--show");
    }

    void HideSettings()
    {
        if (_settingsOverlay != null)
            _settingsOverlay.RemoveFromClassList("overlay--show");
    }

    void SyncSliders()
    {
        var audio = GlobalAudioManager.Instance;
        if (audio == null)
            return;

        _masterSlider?.SetValueWithoutNotify(audio.masterVolume);
        _sfxSlider?.SetValueWithoutNotify(audio.sfxVolume);
        _musicSlider?.SetValueWithoutNotify(audio.musicVolume);
        _vehicleSlider?.SetValueWithoutNotify(audio.vehicleVolume);
    }

    void OnMasterVolumeChanged(ChangeEvent<float> e)
    {
        GlobalAudioManager.Instance?.SetMasterVolume(e.newValue);
    }

    void OnSfxVolumeChanged(ChangeEvent<float> e)
    {
        GlobalAudioManager.Instance?.SetSFXVolume(e.newValue);
    }

    void OnMusicVolumeChanged(ChangeEvent<float> e)
    {
        GlobalAudioManager.Instance?.SetMusicVolume(e.newValue);
    }

    void OnVehicleVolumeChanged(ChangeEvent<float> e)
    {
        GlobalAudioManager.Instance?.SetVehicleVolume(e.newValue);
    }

    void ConfigureSceneShowcase()
    {
        _parkedVan = FindParkedVan();

        if (_parkedVan != null)
        {
            if (applyVanPose)
                _parkedVan.SetPositionAndRotation(vanPosition, Quaternion.Euler(vanEulerAngles));

            PrepareDisplayVan(_parkedVan.gameObject);
            _vanBasePosition = _parkedVan.position;
            _vanBaseRotation = _parkedVan.rotation;
            CacheWheels(_parkedVan.gameObject);
        }

        SetupCamera();
    }

    Transform FindParkedVan()
    {
        var van = GameObject.Find(parkedVanName);
        if (van == null)
            van = GameObject.Find("MainVan");

        return van != null ? van.transform : null;
    }

    static void PrepareDisplayVan(GameObject van)
    {
        if (van == null)
            return;

        foreach (var audioSource in van.GetComponentsInChildren<AudioSource>())
        {
            audioSource.Stop();
            audioSource.enabled = false;
        }

        foreach (var rigidbody in van.GetComponentsInChildren<Rigidbody>())
        {
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;
            rigidbody.linearVelocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
        }

        foreach (var behaviour in van.GetComponents<MonoBehaviour>())
        {
            string behaviourName = behaviour.GetType().Name;
            if (behaviourName == "RCC_CarControllerV4" ||
                behaviourName == "RCC_AICarController" ||
                behaviourName == "AirControl" ||
                behaviourName == "FlipRecovery" ||
                behaviourName == "ObjectAudioManager" ||
                behaviourName == "VehiclePickupAnimator")
            {
                behaviour.enabled = false;
            }
        }
    }

    void CacheWheels(GameObject van)
    {
        _wheels = null;
        _wheelBasePositions = null;
        _wheelBaseRotations = null;

        var car = van.GetComponent<RCC_CarControllerV4>();
        if (car == null)
            return;

        var wheels = new System.Collections.Generic.List<Transform>
        {
            car.FrontLeftWheelTransform,
            car.FrontRightWheelTransform,
            car.RearLeftWheelTransform,
            car.RearRightWheelTransform,
        };

        if (car.ExtraRearWheelsTransform != null)
            wheels.AddRange(car.ExtraRearWheelsTransform);

        wheels.RemoveAll(w => w == null);
        if (wheels.Count == 0)
            return;

        _wheels = wheels.ToArray();
        _wheelBasePositions = new Vector3[_wheels.Length];
        _wheelBaseRotations = new Quaternion[_wheels.Length];
        for (int i = 0; i < _wheels.Length; i++)
        {
            _wheelBasePositions[i] = _wheels[i].position;
            _wheelBaseRotations[i] = _wheels[i].rotation;
        }
    }

    void SetupCamera()
    {
        var cam = menuCamera != null ? menuCamera : Camera.main;
        if (cam == null)
            return;

        if (applyCameraPose)
        {
            cam.transform.position = cameraPosition;
            if (_parkedVan != null)
                cam.transform.LookAt(_parkedVan.position + cameraTargetOffset);

            cam.fieldOfView = cameraFieldOfView;
        }

        cam.clearFlags = CameraClearFlags.Skybox;
    }
}
