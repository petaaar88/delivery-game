using UnityEngine;

[System.Serializable]
public class SoundClip
{
    [Header("Sound Settings")]
    public string soundName;
    public AudioClip audioClip;
    public SoundType soundType = SoundType.SFX;

    [Header("Volume Settings")]
    [Range(0f, 1f)]
    public float individualVolume = 1f;

    [Header("Additional Settings")]
    [Range(0.1f, 3f)]
    public float pitch = 1f;
    public bool loop = false;
    public bool playAtAwake = false;

    [Header("3D Audio Settings")]
    public bool is3D = true;
    [Range(0f, 1f)]
    public float spatialBlend = 1f; // 0 = 2D, 1 = 3D
    public float minDistance = 1f;
    public float maxDistance = 50f;
    public AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;

    [HideInInspector]
    public AudioSource audioSource;
}
