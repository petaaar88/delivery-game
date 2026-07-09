using UnityEngine;
using UnityEngine.Rendering.Universal;

public class FloatingRotation : MonoBehaviour
{
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int UpperBaseColorId = Shader.PropertyToID("_BASE_COLOR");
    static readonly int LegacyColorId = Shader.PropertyToID("_Color");
    static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [Header("Rotation")]
    [SerializeField] private Vector3 rotationSpeed = new Vector3(0f, 45f, 0f);

    [Header("Floating")]
    [SerializeField] private float floatHeight = 0.25f;
    [SerializeField] private float floatSpeed = 1f;

    private Vector3 startPosition;

    private void Awake()
    {
        ApplyPinGlow();
    }

    private void Start()
    {
        startPosition = transform.position;
        EnableBloom();
    }

    private void ApplyPinGlow()
    {
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // Every floating package marker receives its own material instance.
            // This covers all marker variants without changing ordinary props.
            foreach (Material material in renderer.materials)
            {
                if (!material.HasProperty(EmissionColorId))
                    continue;

                material.EnableKeyword("_EMISSION");

                Texture baseMap = material.HasProperty(BaseMapId)
                    ? material.GetTexture(BaseMapId)
                    : null;

                if (baseMap != null)
                {
                    // Textured pins preserve every painted part's own colour.
                    material.SetTexture(EmissionMapId, baseMap);
                    // Keep the bloom threshold just above the surface colour:
                    // this reads vivid rather than being washed out to white.
                    material.SetColor(EmissionColorId, Color.white * 1.08f);
                }
                else
                {
                    // Flat-colour parts (rings, arrows, warning plates) glow
                    // in their own material colour instead of one shared hue.
                    Color baseColor = material.HasProperty(BaseColorId)
                        ? material.GetColor(BaseColorId)
                        : material.GetColor(LegacyColorId);

                    baseColor = SaturatedMarkerColor(material.name, baseColor);
                    if (material.HasProperty(BaseColorId))
                        material.SetColor(BaseColorId, baseColor);
                    if (material.HasProperty(UpperBaseColorId))
                        material.SetColor(UpperBaseColorId, baseColor);
                    if (material.HasProperty(LegacyColorId))
                        material.SetColor(LegacyColorId, baseColor);

                    material.SetColor(EmissionColorId, baseColor * 1.35f);
                }
            }
        }
    }

    private static Color SaturatedMarkerColor(string materialName, Color fallback)
    {
        string name = materialName.ToLowerInvariant();

        if (name.Contains("narandzasta"))
            return WithReducedSaturation(new Color(1f, 0.16f, 0.01f, 1f));
        if (name.Contains("zuta"))
            return WithReducedSaturation(new Color(1f, 0.68f, 0.01f, 1f));
        if (name.Contains("crvena"))
            return WithReducedSaturation(new Color(1f, 0.04f, 0.03f, 1f));
        if (name.Contains("plava"))
            return WithReducedSaturation(new Color(0.02f, 0.38f, 1f, 1f));

        return fallback;
    }

    private static Color WithReducedSaturation(Color color)
    {
        Color.RGBToHSV(color, out float hue, out float saturation, out float value);
        Color tonedDown = Color.HSVToRGB(hue, saturation * 0.85f, value);
        tonedDown.a = color.a;
        return tonedDown;
    }

    private void EnableBloom()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
            return;

        UniversalAdditionalCameraData cameraData = mainCamera.GetComponent<UniversalAdditionalCameraData>();
        if (cameraData != null)
            cameraData.renderPostProcessing = true;
    }

    private void Update()
    {
        // Rotate
        transform.Rotate(rotationSpeed * Time.deltaTime);

        // Float up and down
        float newY = startPosition.y + Mathf.Sin(Time.time * floatSpeed) * floatHeight;
        transform.position = new Vector3(startPosition.x, newY, startPosition.z);
    }
}
