using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Pause overlay + sound settings, part of the HUD UIDocument.
/// Pausing freezes time and audio; sliders drive GlobalAudioManager.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class PauseMenuController : MonoBehaviour
{
    [Tooltip("Scene loaded by the MAIN MENU button.")]
    public string mainMenuScene = "MainMenu";

    public bool IsPaused { get; private set; }

    VisualElement _overlay;
    VisualElement _pauseCard;
    VisualElement _settingsCard;
    Slider _masterSlider;
    Slider _sfxSlider;
    Slider _musicSlider;

    void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;

        _overlay = root.Q("pause-overlay");
        _pauseCard = root.Q("pause-card");
        _settingsCard = root.Q("settings-card");
        _masterSlider = root.Q<Slider>("master-slider");
        _sfxSlider = root.Q<Slider>("sfx-slider");
        _musicSlider = root.Q<Slider>("music-slider");

        root.Q<Button>("pause-button").clicked += Pause;
        root.Q<Button>("resume-button").clicked += Resume;
        root.Q<Button>("restart-button").clicked += Restart;
        root.Q<Button>("settings-button").clicked += ShowSettings;
        root.Q<Button>("settings-back-button").clicked += ShowPauseCard;
        root.Q<Button>("quit-to-menu-button").clicked += QuitToMenu;

        _masterSlider.RegisterValueChangedCallback(e => GlobalAudioManager.Instance?.SetMasterVolume(e.newValue));
        _sfxSlider.RegisterValueChangedCallback(e => GlobalAudioManager.Instance?.SetSFXVolume(e.newValue));
        _musicSlider.RegisterValueChangedCallback(e => GlobalAudioManager.Instance?.SetMusicVolume(e.newValue));
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (IsPaused) Resume();
            else Pause();
        }
    }

    void OnDestroy()
    {
        // Scene unload while paused must not leave the game frozen.
        if (IsPaused)
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }
    }

    public void Pause()
    {
        if (IsPaused) return;
        IsPaused = true;

        Time.timeScale = 0f;
        AudioListener.pause = true;

        SyncSliders();
        ShowPauseCard();
        _overlay.AddToClassList("overlay--show");
    }

    public void Resume()
    {
        if (!IsPaused) return;
        IsPaused = false;

        Time.timeScale = 1f;
        AudioListener.pause = false;
        _overlay.RemoveFromClassList("overlay--show");
    }

    void Restart()
    {
        Resume();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void QuitToMenu()
    {
        Resume();
        SceneManager.LoadScene(mainMenuScene);
    }

    void ShowSettings()
    {
        _pauseCard.style.display = DisplayStyle.None;
        _settingsCard.style.display = DisplayStyle.Flex;
    }

    void ShowPauseCard()
    {
        _settingsCard.style.display = DisplayStyle.None;
        _pauseCard.style.display = DisplayStyle.Flex;
    }

    void SyncSliders()
    {
        var audio = GlobalAudioManager.Instance;
        if (audio == null) return;

        _masterSlider.SetValueWithoutNotify(audio.masterVolume);
        _sfxSlider.SetValueWithoutNotify(audio.sfxVolume);
        _musicSlider.SetValueWithoutNotify(audio.musicVolume);
    }
}
