using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PackageImpactVFX : MonoBehaviour
{
    [System.Serializable]
    public struct SurfaceDust
    {
        [Tooltip("Matched (case-insensitive) against the PhysicsMaterial name under the impact")]
        public string materialNameContains;
        public Color color;
    }

    [Tooltip("Fallback when no surface below matches — warm concrete-grey")]
    public Color dustColor = new(0.58f, 0.54f, 0.48f, 1f);

    [Tooltip("Per-surface dust colors, resolved from the PhysicsMaterial under the impact point")]
    public SurfaceDust[] surfaceColors =
    {
        new() { materialNameContains = "asphalt", color = new Color(0.50f, 0.49f, 0.47f) },
        new() { materialNameContains = "grass",   color = new Color(0.45f, 0.46f, 0.32f) },
        new() { materialNameContains = "sand",    color = new Color(0.72f, 0.63f, 0.46f) },
        new() { materialNameContains = "mud",     color = new Color(0.48f, 0.40f, 0.30f) },
        new() { materialNameContains = "terrain", color = new Color(0.55f, 0.47f, 0.36f) },
    };

    [Tooltip("Layers considered ground when resolving the dust color")]
    public LayerMask groundLayers = ~0;

    [Tooltip("Overall scale of the burst")]
    public float scale = 1f;

    [Tooltip("How strongly the burst leans in the package's travel direction")]
    [Range(0f, 1f)]
    public float directionalBias = 0.45f;

    private ParticleSystem _shockwave;
    private ParticleSystem _dustClouds;
    private ParticleSystem _cardboard;

    private static Texture2D _puffTex;
    private static Texture2D _ringTex;
    private static Material _puffMat;
    private static Material _ringMat;
    private static Material _flakeMat;

    void Awake()
    {
        _shockwave = BuildShockwave();
        _dustClouds = BuildDustClouds();
        _cardboard = BuildCardboardFlecks();
        transform.localScale = Vector3.one * scale;
    }

    public void Play(Vector3 worldPosition) => Play(worldPosition, Vector3.zero);

    public void Play(Vector3 worldPosition, Vector3 impactDirection)
    {
        transform.position = worldPosition;

        // Lean the whole burst along the travel direction — debris kicks forward
        // on an angled impact instead of spraying out in a perfect circle.
        Vector3 flat = impactDirection;
        flat.y = 0f;
        transform.rotation = flat.sqrMagnitude > 0.001f
            ? Quaternion.FromToRotation(Vector3.up, (Vector3.up + flat.normalized * directionalBias).normalized)
            : Quaternion.identity;

        Color ground = ResolveDustColor(worldPosition);
        var shockMain = _shockwave.main;
        shockMain.startColor = new Color(ground.r * 1.1f, ground.g * 1.08f, ground.b * 1.05f, 0.85f);
        var cloudsMain = _dustClouds.main;
        cloudsMain.startColor = DustGradient(ground, 0.6f);

        _shockwave.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _dustClouds.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _cardboard.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        _shockwave.Play();
        _dustClouds.Play();
        _cardboard.Play();
    }

    // ── builders ──────────────────────────────────────────────────────────────

    // Single flat ring sprite that punches outward along the ground — the
    // arcade "impact" read, replacing the old many-particle dust ring.
    ParticleSystem BuildShockwave()
    {
        var go = new GameObject("Shockwave");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0.03f, 0f); // avoid z-fighting the ground
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.1f;
        main.loop = false;
        main.startLifetime = 0.3f;
        main.startSpeed = 0f;
        main.startSize = 2.4f;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.playOnAwake = false;
        main.maxParticles = 1;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

        var shape = ps.shape;
        shape.enabled = false;

        // Fast expansion that eases out as it dissipates.
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 0.15f), new Keyframe(0.35f, 0.75f), new Keyframe(1f, 1f)));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = AlphaFadeGradient(1f, quickIn: true);

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        r.material = RingMaterial();
        r.sortingFudge = 2; // behind the dust
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return ps;
    }

    // Soft plumes that billow upward and swirl with turbulence.
    ParticleSystem BuildDustClouds()
    {
        var go = new GameObject("DustClouds");
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.15f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 0.95f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.34f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = DustGradient(dustColor, 0.6f);
        main.gravityModifier = -0.15f; // gentle rise
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.playOnAwake = false;
        main.maxParticles = 30;

        // A beat after the shockwave — dust billows up out of the initial hit.
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0.05f, 14, 20) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.25f;

        // Organic swirl so the plumes don't look like a uniform expansion.
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        noise.frequency = 0.6f;
        noise.scrollSpeed = 0.5f;
        noise.damping = true;

        var rol = ps.rotationOverLifetime;
        rol.enabled = true;
        rol.z = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = AlphaFadeGradient(0.65f);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(0.4f, 1f), new Keyframe(1f, 1.7f)));

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.material = PuffMaterial();
        r.flip = new Vector3(1f, 1f, 0f); // random UV flips break up the shared texture
        r.sortingFudge = 1;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return ps;
    }

    // Torn cardboard scraps — it's a package hitting the ground, so the debris
    // is packaging, not gravel. Rectangular quads are the point here.
    ParticleSystem BuildCardboardFlecks()
    {
        var go = new GameObject("CardboardFlecks");
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.08f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4.5f);
        main.startSize3D = true;
        main.startSizeX = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
        main.startSizeY = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
        main.startSizeZ = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.72f, 0.55f, 0.36f),
            new Color(0.52f, 0.40f, 0.27f));
        main.gravityModifier = 1.6f; // light — it flutters more than it drops
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.playOnAwake = false;
        main.maxParticles = 30;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 10, 16) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 45f;
        shape.radius = 0.05f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        // Air resistance — scraps decelerate and flutter instead of flying ballistic.
        var lvol = ps.limitVelocityOverLifetime;
        lvol.enabled = true;
        lvol.drag = 0.6f;

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.3f;
        noise.frequency = 1.2f;

        var rol = ps.rotationOverLifetime;
        rol.enabled = true;
        rol.z = new ParticleSystem.MinMaxCurve(-6f, 6f); // fast tumble

        var collision = ps.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.bounce = 0.2f;
        collision.dampen = 0.6f;
        collision.lifetimeLoss = 0.1f;

        // Shrink out at the end of life instead of popping out of existence.
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.65f, 1f), new Keyframe(1f, 0f)));

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.material = FlakeMaterial();
        r.sortingFudge = 0; // in front of the dust
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return ps;
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    Color ResolveDustColor(Vector3 worldPosition)
    {
        if (Physics.Raycast(worldPosition + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 3f,
                groundLayers, QueryTriggerInteraction.Ignore))
        {
            PhysicsMaterial pm = hit.collider.sharedMaterial;
            if (pm != null)
            {
                string n = pm.name.ToLowerInvariant();
                foreach (SurfaceDust s in surfaceColors)
                    if (!string.IsNullOrEmpty(s.materialNameContains) &&
                        n.Contains(s.materialNameContains.ToLowerInvariant()))
                        return s.color;
            }
        }
        return dustColor;
    }

    static ParticleSystem.MinMaxGradient DustGradient(Color c, float alpha) => new(
        new Color(c.r * 1.15f, c.g * 1.05f, c.b * 0.9f, alpha),
        new Color(c.r * 0.85f, c.g * 0.78f, c.b * 0.6f, alpha * 0.7f));

    static ParticleSystem.MinMaxGradient AlphaFadeGradient(float peakAlpha, bool quickIn = false)
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(quickIn ? peakAlpha : 0f, 0f),
                new GradientAlphaKey(peakAlpha, quickIn ? 0.05f : 0.12f),
                new GradientAlphaKey(peakAlpha * 0.4f, 0.55f),
                new GradientAlphaKey(0f, 1f)
            });
        return new ParticleSystem.MinMaxGradient(g);
    }

    static Material PuffMaterial() => _puffMat != null ? _puffMat : _puffMat = MakeParticleMaterial(PuffTexture());
    static Material RingMaterial() => _ringMat != null ? _ringMat : _ringMat = MakeParticleMaterial(RingTexture());
    static Material FlakeMaterial() => _flakeMat != null ? _flakeMat : _flakeMat = MakeParticleMaterial(null);

    static Material MakeParticleMaterial(Texture2D baseMap)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) return null;
        var mat = new Material(sh);

        // URP only applies _Surface/_Blend through its editor GUI, so set the
        // actual render state by hand — otherwise the material stays opaque
        // and every particle renders as a solid quad.
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f); // Alpha
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Transparent;

        // Fade out where quads intersect the ground. Only when the pipeline
        // provides a depth texture — with the keyword on and no depth texture,
        // the fade reads garbage and the particles vanish in the Game view.
        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp &&
            urp.supportsCameraDepthTexture)
        {
            mat.SetFloat("_SoftParticlesEnabled", 1f);
            mat.SetVector("_SoftParticleFadeParams", new Vector4(0f, 1f / 0.35f, 0f, 0f));
            mat.EnableKeyword("_SOFTPARTICLES_ON");
        }

        if (baseMap != null) mat.SetTexture("_BaseMap", baseMap);
        return mat;
    }

    // Soft radial puff broken up with noise so plumes don't all read as the
    // same airbrushed blob.
    static Texture2D PuffTexture()
    {
        if (_puffTex != null) return _puffTex;
        return _puffTex = GenerateTexture((dx, dy, d) =>
        {
            float a = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(d));
            a *= a; // tighten the core, feather the rim
            float n = Mathf.PerlinNoise(dx * 3.5f + 7.3f, dy * 3.5f + 2.9f);
            return a * Mathf.Lerp(0.55f, 1f, n);
        });
    }

    // Ring band with soft inner and outer edges for the shockwave sprite.
    static Texture2D RingTexture()
    {
        if (_ringTex != null) return _ringTex;
        return _ringTex = GenerateTexture((dx, dy, d) =>
        {
            float inner = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 0.75f, d));
            float outer = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.8f, 1f, d));
            return inner * outer;
        });
    }

    static Texture2D GenerateTexture(System.Func<float, float, float, float> alphaAt)
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                byte a = (byte)(Mathf.Clamp01(alphaAt(dx, dy, d)) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }
}
