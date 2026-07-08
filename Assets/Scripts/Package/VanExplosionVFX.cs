using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Cartoon van explosion for the explosive package: flash, fireball, shockwave
/// ring, sparks, smoke column and postal debris. Built entirely in code in the
/// same style as PackageImpactVFX — no external assets or scene wiring; the
/// detonator grabs a shared instance via GetOrCreate(). Also owns the
/// freeze-frame hit-stop and the procedural boom/beep clips, so they survive
/// the detonator component being destroyed mid-explosion.
/// </summary>
public class VanExplosionVFX : MonoBehaviour
{
    [Tooltip("Overall scale of the burst")]
    public float scale = 1f;

    [Header("Hit-stop")]
    [Tooltip("Time.timeScale during the freeze-frame")]
    public float hitStopScale = 0.05f;
    [Tooltip("Real-time seconds the freeze-frame lasts")]
    public float hitStopDuration = 0.09f;

    private ParticleSystem _flash;
    private ParticleSystem _fireball;
    private ParticleSystem _sparks;
    private ParticleSystem _shockwave;
    private ParticleSystem _smoke;
    private ParticleSystem _debris;
    private ParticleSystem _doorPuff;
    private ParticleSystem _doorSparks;

    private static VanExplosionVFX _instance;
    private static Texture2D _puffTex;
    private static Texture2D _ringTex;
    private static Texture2D _flareTex;
    private static Material _puffAlphaMat;
    private static Material _puffAddMat;
    private static Material _ringMat;
    private static Material _flareMat;
    private static Material _flakeMat;
    private static AudioClip _boomClip;
    private static AudioClip _beepClip;

    public static AudioClip BoomClip => _boomClip != null ? _boomClip : _boomClip = GenerateBoom();
    public static AudioClip BeepClip => _beepClip != null ? _beepClip : _beepClip = GenerateBeep();

    public static VanExplosionVFX GetOrCreate()
    {
        if (_instance == null)
        {
            _instance = FindFirstObjectByType<VanExplosionVFX>();
            if (_instance == null)
                _instance = new GameObject("VanExplosionVFX").AddComponent<VanExplosionVFX>();
        }
        return _instance;
    }

    void Awake()
    {
        _flash = BuildFlash();
        _fireball = BuildFireball();
        _sparks = BuildSparks();
        _shockwave = BuildShockwave();
        _smoke = BuildSmokeColumn();
        _debris = BuildPostalDebris();
        _doorPuff = BuildDoorPuff();
        _doorSparks = BuildDoorSparks();
        transform.localScale = Vector3.one * scale;
    }

    public void Play(Vector3 worldPosition)
    {
        transform.position = worldPosition;

        foreach (ParticleSystem ps in new[] { _flash, _fireball, _sparks, _shockwave, _smoke, _debris })
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Play();
        }

        StartCoroutine(HitStop());
    }

    /// <summary>
    /// Small dust-and-sparks burst for a door being blown open. Emit-driven
    /// rather than Play-driven so both doors can burst within the same frame
    /// without clearing each other's particles.
    /// </summary>
    public void PlayDoorBurst(Vector3 position, Vector3 direction)
    {
        direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.up;

        if (!_doorPuff.isPlaying) _doorPuff.Play();
        if (!_doorSparks.isPlaying) _doorSparks.Play();

        var puff = new ParticleSystem.EmitParams();
        for (int i = 0; i < 6; i++)
        {
            puff.position = position + Random.insideUnitSphere * 0.15f;
            puff.velocity = (direction + Random.insideUnitSphere * 0.5f).normalized * Random.Range(1.5f, 3f);
            _doorPuff.Emit(puff, 1);
        }

        var spark = new ParticleSystem.EmitParams();
        for (int i = 0; i < 8; i++)
        {
            spark.position = position;
            spark.velocity = (direction + Random.insideUnitSphere * 0.35f).normalized * Random.Range(6f, 11f);
            _doorSparks.Emit(spark, 1);
        }
    }

    // Brief freeze on the flash frame — cheap and does more for perceived
    // impact than more particles would.
    IEnumerator HitStop()
    {
        if (!Mathf.Approximately(Time.timeScale, 1f))
            yield break; // paused or already mid-effect; don't fight it

        Time.timeScale = hitStopScale;
        yield return new WaitForSecondsRealtime(hitStopDuration);

        // Only restore if nothing else (pause menu) changed it meanwhile.
        if (Mathf.Approximately(Time.timeScale, hitStopScale))
            Time.timeScale = 1f;
    }

    // ── builders ──────────────────────────────────────────────────────────────

    ParticleSystem NewSystem(string name, float yOffset, out ParticleSystemRenderer renderer)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, yOffset, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        renderer = go.GetComponent<ParticleSystemRenderer>();
        return ps;
    }

    // One huge white-hot sprite for ~3 frames. This is what makes it read as a
    // bang rather than a fire starting.
    ParticleSystem BuildFlash()
    {
        var ps = NewSystem("Flash", 0f, out var r);

        var main = ps.main;
        main.duration = 0.1f;
        main.startLifetime = 0.15f;
        main.startSpeed = 0f;
        main.startSize = 7f;
        main.startColor = new Color(1f, 0.97f, 0.85f, 1f);
        main.maxParticles = 1;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

        var shape = ps.shape;
        shape.enabled = false;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.4f, 1f), new Keyframe(1f, 1.1f)));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = AlphaFadeGradient(1f, quickIn: true);

        r.material = FlareMaterial();
        r.sortingFudge = 1;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return ps;
    }

    // Chunky short-lived emissive puffs — cartoon fireballs are fat and brief,
    // not wispy.
    ParticleSystem BuildFireball()
    {
        var ps = NewSystem("Fireball", 0f, out var r);

        var main = ps.main;
        main.duration = 0.1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.1f, 2.1f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = -0.1f;
        main.maxParticles = 20;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.7f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var fire = new Gradient();
        fire.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.95f, 0.75f), 0f),
                new GradientColorKey(new Color(1f, 0.55f, 0.15f), 0.35f),
                new GradientColorKey(new Color(0.55f, 0.14f, 0.05f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.9f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        col.color = new ParticleSystem.MinMaxGradient(fire);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.35f, 1f), new Keyframe(1f, 1.35f)));

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.5f;
        noise.frequency = 0.5f;

        var r2 = r;
        r2.material = PuffAdditiveMaterial();
        r2.flip = new Vector3(1f, 1f, 0f);
        r2.sortingFudge = 1;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return ps;
    }

    // Hot streaks flying out ballistic — stretched billboards along velocity.
    ParticleSystem BuildSparks()
    {
        var ps = NewSystem("Sparks", 0f, out var r);

        var main = ps.main;
        main.duration = 0.08f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.55f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(9f, 16f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.92f, 0.55f), new Color(1f, 0.55f, 0.18f));
        main.gravityModifier = 1f;
        main.maxParticles = 18;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 16) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.4f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = AlphaFadeGradient(1f, quickIn: true);

        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.lengthScale = 5f;
        r.material = FlareMaterial();
        r.sortingFudge = 0;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return ps;
    }

    // Same flat ring read as the package-drop shockwave, scaled way up.
    ParticleSystem BuildShockwave()
    {
        var ps = NewSystem("Shockwave", -1f, out var r);

        var main = ps.main;
        main.duration = 0.1f;
        main.startLifetime = 0.4f;
        main.startSpeed = 0f;
        main.startSize = 10f;
        main.startColor = new Color(1f, 0.78f, 0.45f, 0.85f);
        main.maxParticles = 1;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

        var shape = ps.shape;
        shape.enabled = false;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 0.15f), new Keyframe(0.35f, 0.75f), new Keyframe(1f, 1f)));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = AlphaFadeGradient(0.85f, quickIn: true);

        r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        r.material = RingMaterial();
        r.sortingFudge = 3;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return ps;
    }

    // Dark plumes that outlive the fireball and drift up — the aftermath.
    ParticleSystem BuildSmokeColumn()
    {
        var ps = NewSystem("SmokeColumn", 0f, out var r);

        var main = ps.main;
        main.duration = 0.15f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.4f, 2.6f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.28f, 0.26f, 0.25f, 0.75f), new Color(0.13f, 0.12f, 0.12f, 0.75f));
        main.gravityModifier = -0.12f;
        main.maxParticles = 22;

        // A beat after the flash — smoke boils up out of the fireball.
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0.08f, 16, 20) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 1f;

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(0.5f, 1f);
        noise.frequency = 0.4f;
        noise.scrollSpeed = 0.4f;
        noise.damping = true;

        var rol = ps.rotationOverLifetime;
        rol.enabled = true;
        rol.z = new ParticleSystem.MinMaxCurve(-0.7f, 0.7f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = AlphaFadeGradient(0.75f);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 0.45f), new Keyframe(0.4f, 1f), new Keyframe(1f, 1.8f)));

        r.material = PuffAlphaMaterial();
        r.flip = new Vector3(1f, 1f, 0f);
        r.sortingFudge = 2;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return ps;
    }

    // It's a postal explosion — cardboard scraps and white envelopes rain down,
    // not gravel. Rectangular quads tumbling with drag, like CardboardFlecks
    // but launched much harder.
    ParticleSystem BuildPostalDebris()
    {
        var ps = NewSystem("PostalDebris", 0f, out var r);

        var main = ps.main;
        main.duration = 0.1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 11f);
        main.startSize3D = true;
        main.startSizeX = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        main.startSizeY = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
        main.startSizeZ = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        // Random blend between cardboard brown and envelope white.
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.66f, 0.5f, 0.33f), new Color(0.95f, 0.93f, 0.88f));
        main.gravityModifier = 1.4f;
        main.maxParticles = 26;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0.02f, 20, 26) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 60f;
        shape.radius = 0.6f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        var lvol = ps.limitVelocityOverLifetime;
        lvol.enabled = true;
        lvol.drag = 0.5f;

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.35f;
        noise.frequency = 1f;

        var rol = ps.rotationOverLifetime;
        rol.enabled = true;
        rol.z = new ParticleSystem.MinMaxCurve(-7f, 7f);

        var collision = ps.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.bounce = 0.2f;
        collision.dampen = 0.6f;
        collision.lifetimeLoss = 0.1f;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.7f, 1f), new Keyframe(1f, 0f)));

        r.material = FlakeMaterial();
        r.sortingFudge = 0;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return ps;
    }

    // Little grey-brown puffs knocked off a door as it's blown open. No bursts
    // or shape — particles come only from PlayDoorBurst via Emit, which sets
    // position and velocity per particle.
    ParticleSystem BuildDoorPuff()
    {
        var ps = NewSystem("DoorPuff", 0f, out var r);

        var main = ps.main;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.45f, 0.42f, 0.4f, 0.7f), new Color(0.3f, 0.28f, 0.27f, 0.7f));
        main.gravityModifier = -0.05f;
        main.maxParticles = 30;

        var emission = ps.emission;
        emission.enabled = false;

        var shape = ps.shape;
        shape.enabled = false;

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.3f;
        noise.frequency = 0.8f;
        noise.damping = true;

        var rol = ps.rotationOverLifetime;
        rol.enabled = true;
        rol.z = new ParticleSystem.MinMaxCurve(-1f, 1f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = AlphaFadeGradient(0.7f, quickIn: true);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 1.6f)));

        r.material = PuffAlphaMaterial();
        r.flip = new Vector3(1f, 1f, 0f);
        r.sortingFudge = 1;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return ps;
    }

    // A few hot flecks flying off the hinges with the door puffs — same
    // Emit-driven setup as DoorPuff.
    ParticleSystem BuildDoorSparks()
    {
        var ps = NewSystem("DoorSparks", 0f, out var r);

        var main = ps.main;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.12f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.92f, 0.55f), new Color(1f, 0.55f, 0.18f));
        main.gravityModifier = 1f;
        main.maxParticles = 40;

        var emission = ps.emission;
        emission.enabled = false;

        var shape = ps.shape;
        shape.enabled = false;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = AlphaFadeGradient(1f, quickIn: true);

        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.lengthScale = 4f;
        r.material = FlareMaterial();
        r.sortingFudge = 0;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return ps;
    }

    // ── materials & textures ──────────────────────────────────────────────────

    static Material PuffAlphaMaterial() => _puffAlphaMat != null ? _puffAlphaMat : _puffAlphaMat = MakeParticleMaterial(PuffTexture(), additive: false);
    static Material PuffAdditiveMaterial() => _puffAddMat != null ? _puffAddMat : _puffAddMat = MakeParticleMaterial(PuffTexture(), additive: true);
    static Material RingMaterial() => _ringMat != null ? _ringMat : _ringMat = MakeParticleMaterial(RingTexture(), additive: false);
    static Material FlareMaterial() => _flareMat != null ? _flareMat : _flareMat = MakeParticleMaterial(FlareTexture(), additive: true);
    static Material FlakeMaterial() => _flakeMat != null ? _flakeMat : _flakeMat = MakeParticleMaterial(null, additive: false);

    static Material MakeParticleMaterial(Texture2D baseMap, bool additive)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) return null;
        var mat = new Material(sh);

        // URP only applies _Surface/_Blend through its editor GUI, so set the
        // actual render state by hand — otherwise the material stays opaque
        // and every particle renders as a solid quad.
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", additive ? 2f : 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Transparent;

        // Fade out where quads intersect geometry — only when the pipeline
        // provides a depth texture (see PackageImpactVFX for the why).
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

    static Texture2D PuffTexture()
    {
        if (_puffTex != null) return _puffTex;
        return _puffTex = GenerateTexture((dx, dy, d) =>
        {
            float a = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(d));
            a *= a;
            float n = Mathf.PerlinNoise(dx * 3.5f + 7.3f, dy * 3.5f + 2.9f);
            return a * Mathf.Lerp(0.55f, 1f, n);
        });
    }

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

    // Radial glow with a hot core — for the flash and spark streaks.
    static Texture2D FlareTexture()
    {
        if (_flareTex != null) return _flareTex;
        return _flareTex = GenerateTexture((dx, dy, d) =>
        {
            float glow = Mathf.Pow(Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(d)), 2.2f);
            float core = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0f, 0.3f, d));
            return Mathf.Clamp01(glow * 0.8f + core);
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

    // ── procedural audio ──────────────────────────────────────────────────────

    // Cartoon boom: low-passed noise burst that darkens as it decays, over a
    // sub-bass thump that sweeps down. Placeholder-quality but self-contained;
    // swap for a real clip by registering "ExplosionBoom" on the van's
    // ObjectAudioManager in the inspector.
    static AudioClip GenerateBoom()
    {
        const int sampleRate = 44100;
        const float duration = 1.2f;
        int n = (int)(sampleRate * duration);
        var samples = new float[n];
        var rng = new System.Random(1234);
        float lowPass = 0f;

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / sampleRate;
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            float cutoff = Mathf.Lerp(0.45f, 0.02f, Mathf.Clamp01(t * 1.6f));
            lowPass += (noise - lowPass) * cutoff;

            float freq = Mathf.Lerp(95f, 34f, Mathf.Clamp01(t / 0.35f));
            float thump = Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Exp(-t * 7f);
            float v = lowPass * Mathf.Exp(-t * 5f) * 1.4f + thump * 0.9f;

            samples[i] = (float)System.Math.Tanh(v * 1.6f); // soft clip for punch
        }

        var clip = AudioClip.Create("ExplosionBoom", n, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    // Short electronic fuse beep.
    static AudioClip GenerateBeep()
    {
        const int sampleRate = 44100;
        const float duration = 0.09f;
        const float freq = 1300f;
        int n = (int)(sampleRate * duration);
        var samples = new float[n];

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / sampleRate;
            float attack = Mathf.Clamp01(t / 0.004f);
            float envelope = attack * Mathf.Exp(-t * 30f);
            float tone = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.8f
                       + Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.2f;
            samples[i] = tone * envelope * 0.6f;
        }

        var clip = AudioClip.Create("ExplosiveBeep", n, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
