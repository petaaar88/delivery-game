using UnityEngine;

public class PackageImpactVFX : MonoBehaviour
{
    [Tooltip("Warm-brown for dirt, grey for asphalt")]
    public Color dustColor = new(0.62f, 0.46f, 0.28f, 1f);

    [Tooltip("Overall scale of the burst")]
    public float scale = 1f;

    [Tooltip("How strongly the burst leans in the package's travel direction")]
    [Range(0f, 1f)]
    public float directionalBias = 0.45f;

    private ParticleSystem _dustRing;
    private ParticleSystem _dustClouds;
    private ParticleSystem _pebbles;

    private static Texture2D _softTex;

    void Awake()
    {
        _dustRing = BuildDustRing();
        _dustClouds = BuildDustClouds();
        _pebbles = BuildPebbles();
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

        _dustRing.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _dustClouds.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _pebbles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        _dustRing.Play();
        _dustClouds.Play();
        _pebbles.Play();
    }

    // ── builders ──────────────────────────────────────────────────────────────

    // Flat, ground-hugging shockwave of dust that races outward and settles.
    ParticleSystem BuildDustRing()
    {
        var go = new GameObject("DustRing");
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.15f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.28f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 3.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.13f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = DustGradient(0.85f);
        main.gravityModifier = 0.15f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;
        main.maxParticles = 80;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 40, 55) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 78f;          // nearly flat — fans out along the ground
        shape.radius = 0.08f;
        shape.rotation = new Vector3(-90f, 0f, 0f); // open the cone horizontally

        // Decelerate quickly so the dust skids to a stop instead of flying off.
        var vol = ps.velocityOverLifetime;
        vol.enabled = true;
        vol.space = ParticleSystemSimulationSpace.Local;
        vol.speedModifier = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.4f, 0.15f), new Keyframe(1f, 0f)));

        var rol = ps.rotationOverLifetime;
        rol.enabled = true;
        rol.z = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = AlphaFadeGradient(0.85f);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.3f, 1f), new Keyframe(1f, 1.3f)));

        ApplyParticleMaterial(go, 0);
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
        main.startColor = DustGradient(0.6f);
        main.gravityModifier = -0.15f; // gentle rise
        main.simulationSpace = ParticleSystemSimulationSpace.World;
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

        ApplyParticleMaterial(go, 1);
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return ps;
    }

    // Small chunks that fling out in an arc and fall back under gravity.
    ParticleSystem BuildPebbles()
    {
        var go = new GameObject("Pebbles");
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.08f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.36f, 0.30f, 0.22f),
            new Color(0.58f, 0.50f, 0.38f));
        main.gravityModifier = 3.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;
        main.maxParticles = 30;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12, 20) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 45f;
        shape.radius = 0.05f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        var rol = ps.rotationOverLifetime;
        rol.enabled = true;
        rol.z = new ParticleSystem.MinMaxCurve(-3f, 3f);

        // Bounce off the ground instead of falling through it.
        var collision = ps.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.bounce = 0.45f;
        collision.dampen = 0.3f;
        collision.lifetimeLoss = 0.15f;

        // Shrink out at the end of life instead of popping out of existence.
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.65f, 1f), new Keyframe(1f, 0f)));

        ApplyParticleMaterial(go, 2, soft: false);
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return ps;
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    ParticleSystem.MinMaxGradient DustGradient(float alpha) => new(
        new Color(dustColor.r * 1.15f, dustColor.g * 1.05f, dustColor.b * 0.9f, alpha),
        new Color(dustColor.r * 0.85f, dustColor.g * 0.78f, dustColor.b * 0.6f, alpha * 0.7f));

    static ParticleSystem.MinMaxGradient AlphaFadeGradient(float peakAlpha)
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(peakAlpha, 0.12f),
                new GradientAlphaKey(peakAlpha * 0.5f, 0.55f),
                new GradientAlphaKey(0f, 1f)
            });
        return new ParticleSystem.MinMaxGradient(g);
    }

    static void ApplyParticleMaterial(GameObject go, int sortOffset, bool soft = true)
    {
        if (!go.TryGetComponent(out ParticleSystemRenderer r)) return;
        Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) return;
        var mat = new Material(sh);
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f); // Alpha
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        if (soft) mat.SetTexture("_BaseMap", SoftTexture());
        r.material = mat;
        r.sortingFudge = sortOffset; // dust behind, pebbles in front
    }

    // Soft radial puff so dust reads as a fuzzy blob rather than a hard square.
    static Texture2D SoftTexture()
    {
        if (_softTex != null) return _softTex;

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var pixels = new Color[size * size];
        float center = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                // Smooth falloff to zero at the edge, soft shoulder in the middle.
                float a = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(d));
                a *= a; // tighten the core, feather the rim
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        _softTex = tex;
        return tex;
    }
}
