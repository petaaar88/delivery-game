using System.Collections;
using UnityEngine;

public class AnimalPackageEffect : MonoBehaviour, IPackageEffect
{
    [SerializeField] private float minInterval = 2f;
    [SerializeField] private float maxInterval = 6f;
    [SerializeField] private float shakeTorque = 800f;
    [SerializeField] private float shakeForce = 300f;

    [Header("Sounds")]
    [Tooltip("Played on every shake.")]
    [SerializeField] private string scratchSound = "ScratchSFX";
    [Tooltip("One of these is picked at random when a cat sound plays.")]
    [SerializeField] private string[] catSounds = { "CatSFX1", "CatSFX2", "CatSFX3" };
    [Tooltip("Chance (0-1) that a cat sound plays on any given shake.")]
    [Range(0f, 1f)]
    [SerializeField] private float catChance = 0.5f;

    private Coroutine _shakeRoutine;
    private Rigidbody _rb;
    private ObjectAudioManager _audio;

    public void Activate(RCC_CarControllerV4 car)
    {
        _rb = car.Rigid;
        _audio = car.GetComponent<ObjectAudioManager>();
        _shakeRoutine = StartCoroutine(ShakeRoutine());
        Debug.Log("Animal package picked up — something is moving in there!");
    }

    public void Deactivate()
    {
        if (_shakeRoutine != null)
            StopCoroutine(_shakeRoutine);
        _shakeRoutine = null;
        _rb = null;
        _audio = null;
        Debug.Log("Animal package delivered.");
    }

    IEnumerator ShakeRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(minInterval, maxInterval));

            if (_rb == null) yield break;

            Vector3 torque = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(-0.3f, 0.3f),
                Random.Range(-1f, 1f)
            ).normalized * shakeTorque;

            Vector3 force = new Vector3(
                Random.Range(-1f, 1f),
                0f,
                Random.Range(-1f, 1f)
            ).normalized * shakeForce;

            _rb.AddTorque(torque, ForceMode.Impulse);
            _rb.AddForce(force, ForceMode.Impulse);

            PlayShakeSounds();
        }
    }

    void PlayShakeSounds()
    {
        if (_audio == null) return;

        // Scratch plays on every shake.
        _audio.PlaySoundOneShot(scratchSound);

        // Cat sound only sometimes, and a random variant when it does.
        if (catSounds != null && catSounds.Length > 0 && Random.value < catChance)
            _audio.PlaySoundOneShot(catSounds[Random.Range(0, catSounds.Length)]);
    }
}
