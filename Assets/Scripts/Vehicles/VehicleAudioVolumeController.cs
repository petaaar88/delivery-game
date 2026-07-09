using UnityEngine;
using System.Reflection;

/// <summary>
/// Applies GlobalAudioManager.vehicleVolume (master * vehicle) to every sound RCC
/// generates for the player's car, scaled against each sound's authored baseline so
/// the mix RCC was tuned with is preserved at slider value 1.
///
/// Two groups, because RCC updates them differently:
/// - Engine/wind/brake/gear-shift/crash volumes are read by RCC from public "max
///   volume" fields (per-vehicle for engine, global on RCC_Settings for the rest).
///   Those fields are only ever written by their asset/prefab author, never by RCC's
///   own update, so caching each baseline once and rewriting field = baseline *
///   combined every frame is stable.
/// - Reversing beep, tire skid and flat-tire roll have no such field — RCC recomputes
///   AudioSource.volume directly from live driving state every Update(). Those fields
///   are private, so they're reached via cached reflection, and are multiplied in
///   place from LateUpdate (which Unity guarantees runs after every script's Update,
///   including RCC's) so we scale the value RCC just computed this frame rather than
///   racing it.
///
/// NOS/turbo/exhaust-flame/indicator sounds are intentionally not touched: none of
/// those features are enabled on the player van (useNOS/useTurbo/useExhaustFlame are
/// all off, and it has no RCC_Light), so there is nothing to scale.
/// </summary>
public class VehicleAudioVolumeController : MonoBehaviour
{
    static readonly FieldInfo ReversingSoundField = typeof(RCC_CarControllerV4).GetField("reversingSound", BindingFlags.NonPublic | BindingFlags.Instance);
    static readonly FieldInfo WheelAudioSourceField = typeof(RCC_WheelCollider).GetField("audioSource", BindingFlags.NonPublic | BindingFlags.Instance);
    static readonly FieldInfo WheelFlatSourceField = typeof(RCC_WheelCollider).GetField("flatSource", BindingFlags.NonPublic | BindingFlags.Instance);

    RCC_CarControllerV4 _cachedVehicle;
    RCC_WheelCollider[] _cachedWheels;
    float _baseEngineVolume = -1f;
    float _baseWindVolume = -1f;
    float _baseBrakeVolume = -1f;
    float _baseGearShiftVolume = -1f;
    float _baseCrashVolume = -1f;

    void Update()
    {
        if (GlobalAudioManager.Instance == null)
            return;

        float combined = GlobalAudioManager.Instance.masterVolume * GlobalAudioManager.Instance.vehicleVolume;

        RCC_CarControllerV4 vehicle = RCC_SceneManager.Instance != null ? RCC_SceneManager.Instance.activePlayerVehicle : null;
        if (vehicle != null)
        {
            if (vehicle != _cachedVehicle)
            {
                _cachedVehicle = vehicle;
                _cachedWheels = vehicle.GetComponentsInChildren<RCC_WheelCollider>(true);
                _baseEngineVolume = vehicle.maxEngineSoundVolume;
            }

            vehicle.maxEngineSoundVolume = _baseEngineVolume * combined;
        }

        RCC_Settings settings = RCC_Settings.Instance;
        if (settings != null)
        {
            if (_baseWindVolume < 0f)
                _baseWindVolume = settings.maxWindSoundVolume;
            if (_baseBrakeVolume < 0f)
                _baseBrakeVolume = settings.maxBrakeSoundVolume;
            if (_baseGearShiftVolume < 0f)
                _baseGearShiftVolume = settings.maxGearShiftingSoundVolume;
            if (_baseCrashVolume < 0f)
                _baseCrashVolume = settings.maxCrashSoundVolume;

            settings.maxWindSoundVolume = _baseWindVolume * combined;
            settings.maxBrakeSoundVolume = _baseBrakeVolume * combined;
            settings.maxGearShiftingSoundVolume = _baseGearShiftVolume * combined;
            settings.maxCrashSoundVolume = _baseCrashVolume * combined;
        }
    }

    void LateUpdate()
    {
        if (GlobalAudioManager.Instance == null || _cachedVehicle == null)
            return;

        float combined = GlobalAudioManager.Instance.masterVolume * GlobalAudioManager.Instance.vehicleVolume;

        ScaleInPlace(ReversingSoundField.GetValue(_cachedVehicle) as AudioSource, combined);

        if (_cachedWheels != null)
        {
            foreach (RCC_WheelCollider wheel in _cachedWheels)
            {
                if (wheel == null)
                    continue;

                ScaleInPlace(WheelAudioSourceField.GetValue(wheel) as AudioSource, combined);
                ScaleInPlace(WheelFlatSourceField.GetValue(wheel) as AudioSource, combined);
            }
        }
    }

    static void ScaleInPlace(AudioSource source, float combined)
    {
        if (source != null)
            source.volume *= combined;
    }
}
