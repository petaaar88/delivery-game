using System.Collections;
using UnityEngine;

public class AnimalPackageEffect : MonoBehaviour, IPackageEffect
{
    [SerializeField] private float minInterval = 2f;
    [SerializeField] private float maxInterval = 6f;
    [SerializeField] private float shakeTorque = 800f;
    [SerializeField] private float shakeForce = 300f;

    private Coroutine _shakeRoutine;
    private Rigidbody _rb;

    public void Activate(RCC_CarControllerV4 car)
    {
        _rb = car.Rigid;
        _shakeRoutine = StartCoroutine(ShakeRoutine());
        Debug.Log("Animal package picked up — something is moving in there!");
    }

    public void Deactivate()
    {
        if (_shakeRoutine != null)
            StopCoroutine(_shakeRoutine);
        _shakeRoutine = null;
        _rb = null;
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
        }
    }
}
