using UnityEngine;

public class BoostPanel : MonoBehaviour
{
    [SerializeField] private float boostForce = 25f;
    [SerializeField] private float cooldown = 1f;

    private Renderer _padRenderer;
    private float _flashTimer;
    private float _cooldownTimer;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private void Awake()
    {
        _padRenderer = GetComponentInChildren<Renderer>();
    }

    private void Update()
    {
        if (_cooldownTimer > 0f)
            _cooldownTimer -= Time.deltaTime;

        if (_flashTimer > 0f)
        {
            _flashTimer -= Time.deltaTime;
            if (_flashTimer <= 0f && _padRenderer != null)
                _padRenderer.material.SetColor(EmissionColorId, Color.black);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_cooldownTimer > 0f) return;

        RCC_CarControllerV4 car = other.GetComponentInParent<RCC_CarControllerV4>();
        if (car == null) return;

        car.Rigid.AddForce(car.transform.forward * boostForce, ForceMode.VelocityChange);
        _cooldownTimer = cooldown;

        if (_padRenderer != null)
        {
            _padRenderer.material.SetColor(EmissionColorId, Color.white * 3f);
            _flashTimer = 0.2f;
        }
    }
}
