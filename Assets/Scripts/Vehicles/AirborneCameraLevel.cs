using UnityEngine;

/// <summary>
/// Keeps the TPS follow camera's pitch level so the player still sees ahead
/// while the car is nosed up on a ramp or airborne.
///
/// RCC_Camera derives its pitch from Quaternion.LookRotation(vehicle.forward)
/// whenever TPSLockX is enabled. When the car climbs a ramp its forward points
/// skyward, so the camera pitches up to stare at the sky and the rear of the
/// car (and TPSFreeFall then freezes that framing for the whole jump).
/// Unlocking the camera's X axis keeps the horizon level (only yaw tracks the
/// car), so the view stays forward-facing.
///
/// We react to RCC's OnBCGCameraSpawned event instead of modifying the RCC
/// asset, so this applies to every scene automatically whether the camera is
/// placed in the scene or spawned at runtime.
/// </summary>
public static class AirborneCameraLevel
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        // Static event; the delegate is reset on domain reload, and the handler
        // is idempotent, so re-subscribing each play session is harmless.
        RCC_Camera.OnBCGCameraSpawned -= OnCameraSpawned;
        RCC_Camera.OnBCGCameraSpawned += OnCameraSpawned;
    }

    private static void OnCameraSpawned(GameObject cameraObject)
    {
        if (!cameraObject)
            return;

        RCC_Camera camera = cameraObject.GetComponent<RCC_Camera>();

        if (camera)
            camera.TPSLockX = false;
    }
}
