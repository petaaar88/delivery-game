using System.Collections;
using UnityEngine;

[RequireComponent(typeof(RCC_CarControllerV4))]
public class VehiclePickupAnimator : MonoBehaviour
{
    [Header("Doors")]
    public Transform rightDoor;
    public Transform leftDoor;
    [Range(45f, 135f)]
    public float doorOpenAngle = 90f;
    public float doorAnimDuration = 0.35f;
    public float doorStagger = 0.12f;
    public float doorOvershoot = 7f;
    public float doorSlamRebound = 4f;
    public float doorSettleDuration = 0.18f;

    [Header("Door Sounds")]
    public ObjectAudioManager audioManager;
    public string openDoorSound = "OpeningDoor";
    public string closeDoorSound = "ClosingDoor";

    [Header("Package Sounds")]
    public string packageSpawnSound = "PackageSpawn";
    public string packageHitGroundSound = "PackageHitGround";
    public string packageHitVehicleSound = "PackageHitVehicle";
    public string clipSlideSound = "ClipSlide";

    [Header("Package")]
    public Transform bouncePoint;
    public Transform packageSlot;
    public float archHeight = 2f;
    public float vehicleEntryArchHeight = 1f;
    public float packageFlyDuration = 1.0f;
    public float packageBounceDuration = 0.7f;

    [Header("VFX")]
    public PackageImpactVFX impactVFX;

    [Header("Package Juice")]
    public float anticipationSquash = 0.2f;
    public float anticipationDuration = 0.15f;
    public float spinSpeedMin = 180f;
    public float spinSpeedMax = 420f;
    public float flightStretch = 0.2f;
    public float impactSquash = 0.35f;
    public float impactSquashDuration = 0.09f;
    public float landSquash = 0.25f;
    public float settleDuration = 0.35f;

    [Header("Clip")]
    public Transform clip;
    public Vector3 clipPushDirection = Vector3.back;
    public float clipPushDistance = 1f;
    public float clipPushDuration = 0.4f;
    public float clipReturnDuration = 0.3f;

    [Header("Delivery Juice")]
    public float clipWindupFraction = 0.25f;
    public float clipWindupDuration = 0.15f;
    public float tossDistance = 1.4f;
    public float tossArcHeight = 0.5f;
    public float tossDuration = 0.45f;

    public static event System.Action OnPickupComplete;

    private RCC_CarControllerV4 _car;
    private IPackageEffect _activeEffect;

    void Awake()
    {
        _car = GetComponent<RCC_CarControllerV4>();
        if (audioManager == null)
            audioManager = GetComponent<ObjectAudioManager>();
    }

    public bool HasPackage() => packageSlot.childCount > 0;

    public void StartPickupSequence(Transform package, IPackageEffect effect = null)
    {
        _activeEffect = effect;
        StartCoroutine(PickupSequence(package));
    }

    IEnumerator PickupSequence(Transform package)
    {
        _car.canControl = false;

        yield return StartCoroutine(AnimateDoors(true));
        yield return StartCoroutine(FlyPackage(package));
        yield return StartCoroutine(AnimateDoors(false));

        package.SetParent(packageSlot);
        if (_activeEffect != null) _activeEffect.Activate(_car);
        _car.canControl = true;
        OnPickupComplete?.Invoke();
    }

    IEnumerator AnimateDoors(bool open)
    {
        bool leftFirst = Random.value < 0.5f;
        Coroutine left = StartCoroutine(AnimateSingleDoor(leftDoor, 1f, open, leftFirst ? 0f : doorStagger));
        Coroutine right = StartCoroutine(AnimateSingleDoor(rightDoor, -1f, open, leftFirst ? doorStagger : 0f));
        yield return left;
        yield return right;
    }

    IEnumerator AnimateSingleDoor(Transform door, float sign, bool open, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        Quaternion start = door.localRotation;
        Quaternion target = Quaternion.Euler(0f, open ? sign * doorOpenAngle : 0f, 0f);

        if (open)
        {
            // Fast swing past the open angle, then settle back — like hitting the hinge stop.
            if (audioManager != null)
                audioManager.PlaySoundOneShot(openDoorSound);

            Quaternion overshot = Quaternion.Euler(0f, sign * (doorOpenAngle + doorOvershoot), 0f);
            float elapsed = 0f;
            while (elapsed < doorAnimDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / doorAnimDuration);
                float easeOut = 1f - (1f - t) * (1f - t) * (1f - t);
                door.localRotation = Quaternion.Lerp(start, overshot, easeOut);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < doorSettleDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / doorSettleDuration);
                float smooth = t * t * (3f - 2f * t);
                door.localRotation = Quaternion.Lerp(overshot, target, smooth);
                yield return null;
            }
        }
        else
        {
            // Accelerating slam shut, sound on impact, then a small damped rebound.
            float elapsed = 0f;
            while (elapsed < doorAnimDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / doorAnimDuration);
                float easeIn = t * t * t;
                door.localRotation = Quaternion.Lerp(start, target, easeIn);
                yield return null;
            }

            if (audioManager != null)
                audioManager.PlaySoundOneShot(closeDoorSound);

            elapsed = 0f;
            while (elapsed < doorSettleDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / doorSettleDuration);
                float rebound = sign * doorSlamRebound * Mathf.Sin(t * Mathf.PI) * (1f - t);
                door.localRotation = Quaternion.Euler(0f, rebound, 0f);
                yield return null;
            }
        }

        door.localRotation = target;
    }

    IEnumerator FlyPackage(Transform package)
    {
        package.SetParent(null);
        Vector3 baseScale = package.localScale;

        if (audioManager != null)
            audioManager.PlaySoundOneShot(packageSpawnSound);

        yield return StartCoroutine(SquashPulse(package, baseScale, anticipationSquash, anticipationDuration));

        Vector3 flightStart = package.position;
        yield return StartCoroutine(FlyArcJuicy(package, flightStart, bouncePoint.position, archHeight, packageFlyDuration, baseScale, null));

        if (impactVFX != null)
            impactVFX.Play(bouncePoint.position, bouncePoint.position - flightStart);
        if (audioManager != null)
            audioManager.PlaySoundOneShot(packageHitGroundSound);
        yield return StartCoroutine(SquashPulse(package, baseScale, impactSquash, impactSquashDuration));

        yield return StartCoroutine(FlyArcJuicy(package, bouncePoint.position, packageSlot.position, vehicleEntryArchHeight, packageBounceDuration, baseScale, packageSlot.rotation));

        if (audioManager != null)
            audioManager.PlaySoundOneShot(packageHitVehicleSound);
        yield return StartCoroutine(LandingSettle(package, baseScale));
    }

    // Squash down (and bulge sideways) then return to base — used for launch anticipation and bounce impact.
    IEnumerator SquashPulse(Transform obj, Vector3 baseScale, float amount, float duration)
    {
        if (duration <= 0f || amount <= 0f)
            yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float pulse = Mathf.Sin(t * Mathf.PI) * amount;
            obj.localScale = Vector3.Scale(baseScale, new Vector3(1f + pulse, 1f - pulse, 1f + pulse));
            yield return null;
        }
        obj.localScale = baseScale;
    }

    IEnumerator FlyArcJuicy(Transform obj, Vector3 from, Vector3 to, float height, float duration, Vector3 baseScale, Quaternion? alignTo)
    {
        Quaternion startRot = obj.rotation;
        Vector3 spinAxis = Random.onUnitSphere;
        float spinSpeed = Random.Range(spinSpeedMin, spinSpeedMax);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Linear horizontal travel + parabolic height = real projectile feel with hang time at the apex.
            Vector3 pos = Vector3.Lerp(from, to, t);
            pos.y += height * 4f * t * (1f - t);
            obj.position = pos;

            if (alignTo.HasValue)
                obj.rotation = Quaternion.Slerp(startRot, alignTo.Value, t * t * (3f - 2f * t));
            else
                obj.Rotate(spinAxis, spinSpeed * Time.deltaTime, Space.World);

            // Stretch is strongest at launch/landing (fast) and zero at the apex (slow).
            float speedFactor = Mathf.Abs(1f - 2f * t);
            float stretch = 1f + flightStretch * speedFactor;
            float thin = 1f / Mathf.Sqrt(stretch);
            obj.localScale = Vector3.Scale(baseScale, new Vector3(thin, stretch, thin));

            yield return null;
        }

        obj.position = to;
        if (alignTo.HasValue)
            obj.rotation = alignTo.Value;
        obj.localScale = baseScale;
    }

    // Damped squash-and-overshoot wobble after the package lands in the slot.
    IEnumerator LandingSettle(Transform obj, Vector3 baseScale)
    {
        if (settleDuration <= 0f || landSquash <= 0f)
            yield break;

        float elapsed = 0f;
        while (elapsed < settleDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / settleDuration);
            float damp = (1f - t) * (1f - t);
            float offset = landSquash * Mathf.Cos(t * Mathf.PI * 3f) * damp;
            obj.localScale = Vector3.Scale(baseScale, new Vector3(1f + offset, 1f - offset, 1f + offset));
            yield return null;
        }
        obj.localScale = baseScale;
    }

    /// <summary>
    /// Chaotic door reaction to the package detonating: both rear doors get
    /// blasted open past the hinge stop, rattle in the blast wave, then slam
    /// shut with uneven rebounds. Each door runs on its own random timing so
    /// they never move in sync — deliberately messier than the tidy
    /// pickup/delivery door animation.
    /// </summary>
    public void PlayExplosionDoorBlast()
    {
        StartCoroutine(BlastDoorRoutine(leftDoor, 1f, Random.Range(0f, 0.06f)));
        StartCoroutine(BlastDoorRoutine(rightDoor, -1f, Random.Range(0f, 0.06f)));
    }

    IEnumerator BlastDoorRoutine(Transform door, float sign, float delay)
    {
        if (door == null)
            yield break;
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (audioManager != null)
            audioManager.PlaySoundOneShot(openDoorSound);

        // Dust and sparks knocked off the door as it's blown open. MeshRenderer
        // specifically — a plain Renderer lookup could catch a particle system.
        MeshRenderer doorRend = door.GetComponentInChildren<MeshRenderer>();
        Vector3 burstPos = doorRend != null ? doorRend.bounds.center : door.position;
        Vector3 burstDir = (-transform.forward + Vector3.up * 0.35f).normalized;
        VanExplosionVFX.GetOrCreate().PlayDoorBurst(burstPos, burstDir);

        // Kicked open way past the normal stop, each door a different amount.
        float blastAngle = sign * Mathf.Min(doorOpenAngle + Random.Range(25f, 45f), 150f);
        yield return StartCoroutine(SwingDoor(door, 0f, blastAngle, Random.Range(0.06f, 0.1f), EaseOutCubic));

        // Rattle around the blown-open angle — two incommensurate sine waves
        // per door with random frequency/phase, damping out.
        float rattleTime = Random.Range(0.6f, 0.9f);
        float freq = Random.Range(9f, 14f);
        float amp = Random.Range(12f, 22f);
        float phase = Random.Range(0f, Mathf.PI * 2f);
        float elapsed = 0f;
        while (elapsed < rattleTime)
        {
            elapsed += Time.deltaTime;
            float damp = 1f - Mathf.Clamp01(elapsed / rattleTime);
            damp *= damp;
            float wobble = (Mathf.Sin(elapsed * freq + phase) * amp
                          + Mathf.Sin(elapsed * freq * 2.7f) * amp * 0.4f) * damp;
            door.localRotation = Quaternion.Euler(0f, blastAngle + sign * wobble, 0f);
            yield return null;
        }

        // Doors don't decide to close together.
        yield return new WaitForSeconds(Random.Range(0f, 0.25f));

        // Accelerating slam, then two shrinking rebounds off the frame.
        yield return StartCoroutine(SwingDoor(door, blastAngle, 0f, Random.Range(0.18f, 0.3f), EaseInCubic));
        if (audioManager != null)
            audioManager.PlaySoundOneShot(closeDoorSound);

        float bounce = sign * Random.Range(18f, 32f);
        for (int i = 0; i < 2; i++)
        {
            yield return StartCoroutine(SwingDoor(door, 0f, bounce, Random.Range(0.08f, 0.13f), EaseOutCubic));
            yield return StartCoroutine(SwingDoor(door, bounce, 0f, Random.Range(0.1f, 0.16f), EaseInCubic));
            bounce *= 0.35f;
        }
        if (audioManager != null)
            audioManager.PlaySoundOneShot(closeDoorSound);

        door.localRotation = Quaternion.identity;
    }

    IEnumerator SwingDoor(Transform door, float fromY, float toY, float duration, System.Func<float, float> ease)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = ease(Mathf.Clamp01(elapsed / duration));
            door.localRotation = Quaternion.Euler(0f, Mathf.LerpUnclamped(fromY, toY, t), 0f);
            yield return null;
        }
        door.localRotation = Quaternion.Euler(0f, toY, 0f);
    }

    /// <summary>
    /// Aborts the active package effect outside the normal delivery flow —
    /// e.g. the explosive package detonating destroys the package without a
    /// delivery sequence ever running.
    /// </summary>
    public void ClearActiveEffect()
    {
        if (_activeEffect != null)
        {
            _activeEffect.Deactivate();
            _activeEffect = null;
        }
    }

    public void StartDeliverySequence(System.Action<Transform> onPushComplete = null)
    {
        StartCoroutine(DeliverySequence(onPushComplete));
    }

    IEnumerator DeliverySequence(System.Action<Transform> onPushComplete)
    {
        if (packageSlot.childCount == 0)
            yield break;

        Transform package = packageSlot.GetChild(0);
        if (_activeEffect != null) { _activeEffect.Deactivate(); _activeEffect = null; }
        _car.canControl = false;

        yield return StartCoroutine(AnimateDoors(true));
        yield return StartCoroutine(PushPackage(package, onPushComplete));
        yield return StartCoroutine(AnimateDoors(false));

        _car.canControl = true;
    }

    IEnumerator PushPackage(Transform package, System.Action<Transform> onPushComplete)
    {
        Vector3 clipLocalStart = clip.localPosition;
        Vector3 clipLocalWindup = clipLocalStart - clipPushDirection * (clipPushDistance * clipWindupFraction);
        Vector3 clipLocalEnd = clipLocalStart + clipPushDirection * clipPushDistance;
        Vector3 baseScale = package.localScale;

        package.SetParent(clip);

        // Wind-up pull back, then a snappy ease-out shove.
        yield return StartCoroutine(AnimateLocalPosition(clip, clipLocalStart, clipLocalWindup, clipWindupDuration, EaseSmooth));
        if (audioManager != null)
            audioManager.PlaySoundOneShot(clipSlideSound);
        yield return StartCoroutine(AnimateLocalPosition(clip, clipLocalWindup, clipLocalEnd, clipPushDuration, EaseOutCubic));

        package.SetParent(null);

        // Clip slides home while the package carries the shove's momentum out the back.
        Coroutine clipReturn = StartCoroutine(AnimateLocalPosition(clip, clipLocalEnd, clipLocalStart, clipReturnDuration, EaseSmooth));

        Vector3 pushDirWorld = clip.parent != null ? clip.parent.TransformDirection(clipPushDirection) : clipPushDirection;
        Vector3 tossStart = package.position;
        Vector3 tossEnd = tossStart + pushDirWorld.normalized * tossDistance;

        // Measure the package's own mesh height — NOT GetComponentInChildren<Renderer>(), which
        // would return the attached smoke's particle renderer and its huge (cloud-sized) bounds.
        float clearance = 0f;
        MeshRenderer rend = package.GetComponentInChildren<MeshRenderer>();
        if (rend != null)
            clearance = rend.bounds.extents.y;
        // Drop the package down to the ground next to the car — same height as the wheels'
        // contact point — so it lands low instead of hovering at the clip's height.
        tossEnd.y = GetWheelGroundLevel() + clearance;

        yield return StartCoroutine(FlyArcJuicy(package, tossStart, tossEnd, tossArcHeight, tossDuration, baseScale, null));
        yield return StartCoroutine(LandingSettle(package, baseScale));


        onPushComplete?.Invoke(package);

        yield return clipReturn;
    }

    // Actual road-surface height under the car, taken from the rear wheels' ground-contact points.
    float GetWheelGroundLevel()
    {
        float sum = 0f;
        int count = 0;
        foreach (RCC_WheelCollider w in new[] { _car.RearLeftWheelCollider, _car.RearRightWheelCollider })
        {
            if (w != null && w.isGrounded && w.wheelHit.point != Vector3.zero)
            {
                sum += w.wheelHit.point.y;
                count++;
            }
        }
        if (count > 0)
            return sum / count;

        // Fallback: raycast straight down under the car, ignoring the vehicle's own layers.
        int mask = ~(1 << 8 | 1 << 9 | 1 << 10 | 1 << 11);
        if (Physics.Raycast(transform.position + Vector3.up, Vector3.down, out RaycastHit hit, 20f, mask, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return transform.position.y;
    }

    static float EaseSmooth(float t) => t * t * (3f - 2f * t);
    static float EaseOutCubic(float t) => 1f - (1f - t) * (1f - t) * (1f - t);
    static float EaseInCubic(float t) => t * t * t;

    IEnumerator AnimateLocalPosition(Transform target, Vector3 from, Vector3 to, float duration, System.Func<float, float> ease)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            target.localPosition = Vector3.Lerp(from, to, ease(t));
            yield return null;
        }
        target.localPosition = to;
    }
}
