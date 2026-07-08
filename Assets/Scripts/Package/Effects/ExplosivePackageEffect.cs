using UnityEngine;

/// <summary>
/// While the explosive package is aboard, the van carries a fuse
/// (ExplosiveCollisionDetector): hard collisions advance it and the last one
/// detonates the package, failing the delivery. All fuse/explosion tunables
/// live here because the detector itself is added to the van at runtime.
/// </summary>
public class ExplosivePackageEffect : MonoBehaviour, IPackageEffect
{
    [Header("Fuse")]
    [Tooltip("Hard hits before the package detonates (the last hit is the boom).")]
    public int hitsToExplode = 3;
    [Tooltip("Minimum collision impulse (N·s) for a hit to count.")]
    public float armImpulseThreshold = 7000f;
    [Tooltip("Seconds between hits that can count — stops one crash registering twice.")]
    public float hitCooldown = 1f;
    [Tooltip("Contact normals with a Y component above this are treated as landings and ignored.")]
    [Range(0f, 1f)]
    public float maxGroundNormalY = 0.7f;
    [Tooltip("Seconds between warning beeps after the first strike; shrinks with each extra strike.")]
    public float beepInterval = 0.9f;

    [Header("Explosion")]
    [Tooltip("Upward velocity change (m/s) applied to the van when it blows.")]
    public float upwardKick = 7f;
    [Tooltip("Angular velocity change (rad/s) for the comedy tumble.")]
    public float tumbleKick = 2.5f;
    [Tooltip("Fallback explosion centre height above the van root — only used when the van has no package slot (the blast normally comes from the cargo bay).")]
    public float explosionHeight = 1.3f;

    private ExplosiveCollisionDetector _detector;

    public void Activate(RCC_CarControllerV4 car)
    {
        _detector = car.gameObject.AddComponent<ExplosiveCollisionDetector>();
        _detector.Init(this, car);
        Debug.Log("Explosive package loaded — drive carefully.");
    }

    public void Deactivate()
    {
        if (_detector != null)
        {
            _detector.Cleanup();
            Destroy(_detector);
        }
        _detector = null;
    }
}
