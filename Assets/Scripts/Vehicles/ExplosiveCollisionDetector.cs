using System.Collections;
using UnityEngine;

/// <summary>
/// Fuse-model detonator that ExplosivePackageEffect attaches to the van while
/// an explosive package is aboard. Hard hits advance the fuse — the package
/// beeps faster and pulses red with each strike — and the final strike sets
/// it off: explosion VFX, a comedic physics punt, camera kick and a failed
/// delivery. Tunables live on ExplosivePackageEffect (this component is added
/// at runtime, so it has no inspector of its own).
/// </summary>
public class ExplosiveCollisionDetector : MonoBehaviour
{
    public const string BeepSound = "ExplosiveBeep";
    public const string BoomSound = "ExplosionBoom";

    private ExplosivePackageEffect _config;
    private RCC_CarControllerV4 _car;
    private ObjectAudioManager _audio;
    private VehiclePickupAnimator _animator;

    private int _strikes;
    private float _lastHitTime = -999f;
    private bool _exploded;
    private Coroutine _beepRoutine;
    private Coroutine _pulseRoutine;

    private Transform _package;
    private Vector3 _packageBaseScale;
    private Renderer _packageRenderer;
    private Color _packageBaseColor;

    public void Init(ExplosivePackageEffect config, RCC_CarControllerV4 car)
    {
        _config = config;
        _car = car;
        _audio = car.GetComponent<ObjectAudioManager>();
        _animator = car.GetComponent<VehiclePickupAnimator>();

        if (_audio != null)
        {
            if (!_audio.HasSound(BeepSound))
                _audio.AddSound(BeepSound, VanExplosionVFX.BeepClip, SoundType.SFX, 0.55f);
            if (!_audio.HasSound(BoomSound))
                _audio.AddSound(BoomSound, VanExplosionVFX.BoomClip, SoundType.SFX, 1f);
        }

        if (_animator != null && _animator.packageSlot != null && _animator.packageSlot.childCount > 0)
        {
            _package = _animator.packageSlot.GetChild(0);
            _packageBaseScale = _package.localScale;
            _packageRenderer = _package.GetComponentInChildren<MeshRenderer>();
            if (_packageRenderer != null)
                _packageBaseColor = _packageRenderer.material.color;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (_exploded || _config == null)
            return;
        if (Time.time - _lastHitTime < _config.hitCooldown)
            return;
        if (collision.impulse.magnitude < _config.armImpulseThreshold)
            return;
        // Landing after a jump pushes straight up through the wheels — only
        // count hits whose contact normal is mostly horizontal (walls, cars).
        if (collision.GetContact(0).normal.y > _config.maxGroundNormalY)
            return;

        _lastHitTime = Time.time;
        _strikes++;
        Debug.Log($"Explosive package strike {_strikes}/{_config.hitsToExplode} (impulse {collision.impulse.magnitude:F0})");

        if (_strikes >= _config.hitsToExplode)
        {
            Explode(collision);
            return;
        }

        Beep();
        if (_beepRoutine == null)
            _beepRoutine = StartCoroutine(BeepRoutine());
    }

    // Continuous warning once armed; the interval shrinks with every strike so
    // the beeping gets frantic as the fuse runs out.
    IEnumerator BeepRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(_config.beepInterval / Mathf.Max(1, _strikes));
            Beep();
        }
    }

    void Beep()
    {
        if (_audio != null)
            _audio.PlaySoundOneShot(BeepSound);

        if (_pulseRoutine != null)
            StopCoroutine(_pulseRoutine);
        _pulseRoutine = StartCoroutine(PulsePackage());
    }

    // Quick red flash + scale punch on the package in the cargo bay.
    IEnumerator PulsePackage()
    {
        if (_package == null)
            yield break;

        const float duration = 0.16f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float pulse = Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI);
            if (_package != null)
                _package.localScale = _packageBaseScale * (1f + 0.18f * pulse);
            if (_packageRenderer != null)
                _packageRenderer.material.color = Color.Lerp(_packageBaseColor, Color.red, pulse * 0.85f);
            yield return null;
        }
        RestorePackageVisuals();
    }

    void Explode(Collision collision)
    {
        _exploded = true;

        // The bomb sits in the cargo bay, so the blast comes from the package
        // slot at the back — not the van's centre.
        Vector3 center = _animator != null && _animator.packageSlot != null
            ? _animator.packageSlot.position
            : transform.position + transform.up * _config.explosionHeight;

        // VFX owns the hit-stop coroutine — this component is destroyed below,
        // which would kill any coroutine started here mid-freeze.
        VanExplosionVFX.GetOrCreate().Play(center);

        if (_audio != null)
            _audio.PlaySoundOneShot(BoomSound);

        if (_animator != null)
            _animator.PlayExplosionDoorBlast();

        // Punt the van — the comedy beat. FlipRecovery rights it afterwards.
        Rigidbody rb = _car != null ? _car.Rigid : GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.AddForce(Vector3.up * _config.upwardKick, ForceMode.VelocityChange);
            rb.AddTorque(Random.onUnitSphere * _config.tumbleKick, ForceMode.VelocityChange);
        }

        // Extra camera kick on top of RCC's own collision reaction.
        if (RCC_SceneManager.Instance != null && RCC_SceneManager.Instance.activePlayerCamera != null)
            RCC_SceneManager.Instance.activePlayerCamera.Collision(collision);

        // Deactivate() (via the animator) stops the fuse and destroys this
        // component; the manager then destroys the package and resets the run.
        if (_animator != null)
            _animator.ClearActiveEffect();
        DeliveryManager.Instance?.OnPackageDestroyed();
    }

    /// <summary>Stops the fuse and restores the package's look. Called by
    /// ExplosivePackageEffect.Deactivate before this component is destroyed.</summary>
    public void Cleanup()
    {
        if (_beepRoutine != null) StopCoroutine(_beepRoutine);
        if (_pulseRoutine != null) StopCoroutine(_pulseRoutine);
        _beepRoutine = null;
        _pulseRoutine = null;
        RestorePackageVisuals();
    }

    void RestorePackageVisuals()
    {
        if (_package != null)
            _package.localScale = _packageBaseScale;
        if (_packageRenderer != null)
            _packageRenderer.material.color = _packageBaseColor;
    }
}
