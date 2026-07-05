using UnityEngine;

/// <summary>
/// Shrinks the audible radius of a car's RCC-generated AudioSources so only
/// vehicles near the player can be heard. RCC bakes its min/max distances into
/// code (engine ~50 units, etc.), so this overrides them at runtime without
/// touching the RCC asset. Attach to each vehicle (or the vehicle prefab root).
/// </summary>
public class RCCAudioDistanceOverride : MonoBehaviour
{
    [Header("Hearing Range")]
    [Tooltip("Beyond this distance (in units) the car is inaudible. Lower = only nearby vehicles are heard.")]
    public float maxDistance = 35f;

    [Tooltip("Within this distance the sound stays at full volume.")]
    public float minDistance = 5f;

    [Header("Rolloff")]
    [Tooltip("Linear gives a clean cutoff at Max Distance (recommended for 'only nearby cars'). " +
             "Logarithmic keeps RCC's default falloff, which has a long faint tail.")]
    public bool useLinearRolloff = true;

    void Start()
    {
        // RCC creates its AudioSources during its own Awake/Start, so apply on
        // the next frame to guarantee they exist.
        Invoke(nameof(ApplyDistances), 0f);
    }

    /// <summary>Re-applies the distances. Call again if RCC recreates sources at runtime.</summary>
    public void ApplyDistances()
    {
        foreach (AudioSource src in GetComponentsInChildren<AudioSource>(true))
        {
            src.minDistance = minDistance;
            src.maxDistance = maxDistance;

            if (useLinearRolloff)
                src.rolloffMode = AudioRolloffMode.Linear;
        }
    }
}
