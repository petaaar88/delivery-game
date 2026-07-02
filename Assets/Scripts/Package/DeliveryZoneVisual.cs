using System.Collections;
using UnityEngine;

/// <summary>
/// Drives the animated ring + light beam visuals of a pickup/delivery trigger zone.
/// Sizes are given in world units and compensated for parent scale, so the same
/// setup works on any zone regardless of its transform.
/// </summary>
[ExecuteAlways]
public class DeliveryZoneVisual : MonoBehaviour
{
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int IntensityId = Shader.PropertyToID("_Intensity");

    [Header("Look")]
    public Color zoneColor = new Color(0.2f, 1f, 0.4f, 1f);
    public float ringIntensity = 1.6f;
    public float beamIntensity = 1f;

    [Header("World-space layout")]
    public float worldRadius = 11f;
    public float beamHeight = 8f;
    [Range(0.1f, 1f)] public float beamRadiusScale = 0.35f;
    public float groundClearance = 0.08f;

    [Header("References")]
    public MeshRenderer ringRenderer;
    public MeshRenderer beamRenderer;

    private MaterialPropertyBlock _block;

    void OnEnable()
    {
        ApplyLook();
        ApplyLayout();
    }

    void OnValidate()
    {
        ApplyLook();
        ApplyLayout();
    }

#if UNITY_EDITOR
    // Keep edit-mode visuals in sync with changes made outside the Inspector
    void Update()
    {
        if (!Application.isPlaying)
        {
            ApplyLook();
            ApplyLayout();
        }
    }
#endif

    public void SetColor(Color color)
    {
        zoneColor = color;
        ApplyLook();
    }

    /// <summary>One-shot intensity flash, e.g. on successful delivery.</summary>
    public void PlayBurst(float strength = 3f, float duration = 0.5f)
    {
        if (!Application.isPlaying)
            return;
        StopAllCoroutines();
        StartCoroutine(Burst(strength, duration));
    }

    IEnumerator Burst(float strength, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            ApplyLook(Mathf.Lerp(strength, 1f, elapsed / duration));
            yield return null;
        }
        ApplyLook();
    }

    void ApplyLook(float boost = 1f)
    {
        if (_block == null)
            _block = new MaterialPropertyBlock();
        Apply(ringRenderer, ringIntensity * boost);
        Apply(beamRenderer, beamIntensity * boost);
    }

    void Apply(MeshRenderer target, float intensity)
    {
        if (target == null)
            return;
        target.GetPropertyBlock(_block);
        _block.SetColor(ColorId, zoneColor);
        _block.SetFloat(IntensityId, intensity);
        target.SetPropertyBlock(_block);
    }

    void ApplyLayout()
    {
        Vector3 scale = transform.lossyScale;
        if (scale.x == 0f || scale.y == 0f || scale.z == 0f)
            return;

        float groundY = SampleGroundY();

        if (ringRenderer != null)
        {
            Transform ring = ringRenderer.transform;
            ring.rotation = Quaternion.Euler(90f, 0f, 0f);
            ring.position = new Vector3(transform.position.x, groundY + groundClearance, transform.position.z);
            ring.localScale = new Vector3(2f * worldRadius / scale.x, 2f * worldRadius / scale.z, 1f);
        }

        if (beamRenderer != null)
        {
            Transform beam = beamRenderer.transform;
            beam.rotation = Quaternion.identity;
            beam.position = new Vector3(transform.position.x, groundY + beamHeight * 0.5f, transform.position.z);
            // Unity's cylinder is 2 units tall and 1 unit wide at scale 1
            float beamDiameter = 2f * worldRadius * beamRadiusScale;
            beam.localScale = new Vector3(beamDiameter / scale.x, beamHeight / (2f * scale.y), beamDiameter / scale.z);
        }
    }

    float SampleGroundY()
    {
        Vector3 origin = transform.position + Vector3.up * 30f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 100f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return transform.position.y;
    }
}
