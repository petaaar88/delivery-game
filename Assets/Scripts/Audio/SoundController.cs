using UnityEngine;
using UnityEngine.UI;

public class SoundController : MonoBehaviour
{
    [Header("Volume Sliders")]
    public Slider masterVolumeSlider;
    public Slider sfxVolumeSlider;
    public Slider musicVolumeSlider;

    [Header("Optional: Volume Display")]
    public TMPro.TextMeshProUGUI masterVolumeText;
    public TMPro.TextMeshProUGUI sfxVolumeText;
    public TMPro.TextMeshProUGUI musicVolumeText;

    void Start()
    {
        if (GlobalAudioManager.Instance == null)
        {
            Debug.LogError("GlobalAudioManager not found! Please create a GameObject with GlobalAudioManager script.");
            return;
        }

        // Postaviti početne vrednosti slider-a
        InitializeSliders();

        // Dodeli event listener-e za slider-e
        SetupSliderListeners();

        // Početno ažuriranje teksta
        UpdateVolumeTexts();
    }

    void InitializeSliders()
    {
        if (masterVolumeSlider != null)
            masterVolumeSlider.value = GlobalAudioManager.Instance.masterVolume;

        if (sfxVolumeSlider != null)
            sfxVolumeSlider.value = GlobalAudioManager.Instance.sfxVolume;

        if (musicVolumeSlider != null)
            musicVolumeSlider.value = GlobalAudioManager.Instance.musicVolume;
    }

    void SetupSliderListeners()
    {
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        }
    }

    public void OnMasterVolumeChanged(float value)
    {
        if (GlobalAudioManager.Instance != null)
        {
            GlobalAudioManager.Instance.SetMasterVolume(value);
            UpdateVolumeTexts();
        }
    }

    public void OnSFXVolumeChanged(float value)
    {
        if (GlobalAudioManager.Instance != null)
        {
            GlobalAudioManager.Instance.SetSFXVolume(value);
            UpdateVolumeTexts();
        }
    }

    public void OnMusicVolumeChanged(float value)
    {
        if (GlobalAudioManager.Instance != null)
        {
            GlobalAudioManager.Instance.SetMusicVolume(value);
            UpdateVolumeTexts();
        }
    }

    void UpdateVolumeTexts()
    {
        if (GlobalAudioManager.Instance != null)
        {
            if (masterVolumeText != null)
                masterVolumeText.text = $"Master: {(GlobalAudioManager.Instance.masterVolume * 100):F0}%";

            if (sfxVolumeText != null)
                sfxVolumeText.text = $"SFX: {(GlobalAudioManager.Instance.sfxVolume * 100):F0}%";

            if (musicVolumeText != null)
                musicVolumeText.text = $"Music: {(GlobalAudioManager.Instance.musicVolume * 100):F0}%";
        }
    }

    // Dodatne korisne metode
    public void ResetAllVolumes()
    {
        OnMasterVolumeChanged(1f);
        OnSFXVolumeChanged(1f);
        OnMusicVolumeChanged(1f);

        if (masterVolumeSlider != null) masterVolumeSlider.value = 1f;
        if (sfxVolumeSlider != null) sfxVolumeSlider.value = 1f;
        if (musicVolumeSlider != null) musicVolumeSlider.value = 1f;
    }

    public void MuteAll()
    {
        OnMasterVolumeChanged(0f);
        if (masterVolumeSlider != null) masterVolumeSlider.value = 0f;
    }
}
