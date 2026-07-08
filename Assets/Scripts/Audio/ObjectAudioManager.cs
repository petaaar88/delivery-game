using UnityEngine;
using System.Collections.Generic;

public class ObjectAudioManager : MonoBehaviour
{
    [Header("Sound Collections")]
    public List<SoundClip> soundClips = new List<SoundClip>();

    private Dictionary<string, SoundClip> soundDictionary;

    void Awake()
    {
        // Kreiranje Audio Source komponenti za svaki zvuk
        foreach (SoundClip sound in soundClips)
        {
            if (sound.audioClip != null)
            {
                sound.audioSource = gameObject.AddComponent<AudioSource>();
                sound.audioSource.clip = sound.audioClip;
                sound.audioSource.pitch = sound.pitch;
                sound.audioSource.loop = sound.loop;

                if (sound.audioSource != null && sound.playAtAwake)
                    sound.audioSource.Play();
                else
                    sound.audioSource.playOnAwake = false;

                // 3D Audio setup
                Setup3DAudio(sound);
            }
        }

        // Kreiranje dictionary-ja za brži pristup zvukovima po imenu
        CreateSoundDictionary();
    }

    void Setup3DAudio(SoundClip sound)
    {
        if (sound.is3D)
        {
            sound.audioSource.spatialBlend = sound.spatialBlend;
            sound.audioSource.minDistance = sound.minDistance;
            sound.audioSource.maxDistance = sound.maxDistance;
            sound.audioSource.rolloffMode = sound.rolloffMode;

            // Dodatne 3D postavke
            sound.audioSource.dopplerLevel = 1f; // Doppler efekat
            sound.audioSource.spread = 0f; // Direktionalnost (0 = usmeren, 360 = svuda)
        }
        else
        {
            // 2D zvuk
            sound.audioSource.spatialBlend = 0f;
        }
    }

    void Start()
    {
        // Registruj se kod GlobalAudioManager-a
        if (GlobalAudioManager.Instance != null)
        {
            GlobalAudioManager.Instance.RegisterAudioManager(this);
        }

        UpdateVolumes();
    }

    void OnDestroy()
    {
        // Odregistruj se kada se objekt uništi
        if (GlobalAudioManager.Instance != null)
        {
            GlobalAudioManager.Instance.UnregisterAudioManager(this);
        }
    }

    void CreateSoundDictionary()
    {
        soundDictionary = new Dictionary<string, SoundClip>();
        foreach (SoundClip sound in soundClips)
        {
            if (!string.IsNullOrEmpty(sound.soundName))
            {
                if (!soundDictionary.ContainsKey(sound.soundName))
                {
                    soundDictionary.Add(sound.soundName, sound);
                }
                else
                {
                    Debug.LogWarning($"Duplicate sound name found on {gameObject.name}: {sound.soundName}. Only the first one will be used.");
                }
            }
        }
    }

    public void PlaySound(string soundName)
    {
        if (soundDictionary.ContainsKey(soundName))
        {
            SoundClip sound = soundDictionary[soundName];
            if (sound.audioSource != null)
            {
                sound.audioSource.Play();
            }
        }
        else
        {
            Debug.LogWarning($"Sound with name '{soundName}' not found on {gameObject.name}!");
        }
    }

    public void StopSound(string soundName)
    {
        if (soundDictionary.ContainsKey(soundName))
        {
            SoundClip sound = soundDictionary[soundName];
            if (sound.audioSource != null)
            {
                sound.audioSource.Stop();
            }
        }
        else
        {
            Debug.LogWarning($"Sound with name '{soundName}' not found on {gameObject.name}!");
        }
    }

    public void PlaySoundOneShot(string soundName)
    {
        if (soundDictionary.ContainsKey(soundName))
        {
            SoundClip sound = soundDictionary[soundName];
            if (sound.audioSource != null)
            {
                float finalVolume = CalculateFinalVolume(sound);
                sound.audioSource.PlayOneShot(sound.audioClip, finalVolume);
            }
        }
        else
        {
            Debug.LogWarning($"Sound with name '{soundName}' not found on {gameObject.name}!");
        }
    }

    public void UpdateVolumes()
    {
        foreach (SoundClip sound in soundClips)
        {
            if (sound.audioSource != null)
            {
                sound.audioSource.volume = CalculateFinalVolume(sound);
            }
        }
    }

    float CalculateFinalVolume(SoundClip sound)
    {
        if (GlobalAudioManager.Instance != null)
        {
            float globalVolume = GlobalAudioManager.Instance.GetVolumeForType(sound.soundType);
            return globalVolume * sound.individualVolume;
        }
        return sound.individualVolume;
    }

    // Korisne metode
    public bool HasSound(string soundName)
    {
        return soundDictionary != null && soundDictionary.ContainsKey(soundName);
    }

    public bool IsSoundPlaying(string soundName)
    {
        if (soundDictionary.ContainsKey(soundName))
        {
            return soundDictionary[soundName].audioSource.isPlaying;
        }
        return false;
    }

    public void StopAllSounds()
    {
        foreach (SoundClip sound in soundClips)
        {
            if (sound.audioSource != null && sound.audioSource.isPlaying)
            {
                sound.audioSource.Stop();
            }
        }
    }

    // Dodavanje novog zvuka runtime
    public void AddSound(string name, AudioClip clip, SoundType type, float individualVolume = 1f, bool is3D = true)
    {
        SoundClip newSound = new SoundClip
        {
            soundName = name,
            audioClip = clip,
            soundType = type,
            individualVolume = individualVolume,
            pitch = 1f,
            loop = false,
            is3D = is3D,
            spatialBlend = is3D ? 1f : 0f,
            minDistance = 1f,
            maxDistance = 50f,
            rolloffMode = AudioRolloffMode.Logarithmic
        };

        newSound.audioSource = gameObject.AddComponent<AudioSource>();
        newSound.audioSource.clip = newSound.audioClip;
        newSound.audioSource.pitch = newSound.pitch;
        newSound.audioSource.loop = newSound.loop;
        newSound.audioSource.playOnAwake = false;
        newSound.audioSource.volume = CalculateFinalVolume(newSound);

        Setup3DAudio(newSound);

        soundClips.Add(newSound);
        soundDictionary[name] = newSound;
    }
}
