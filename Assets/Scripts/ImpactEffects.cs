using System.Collections;
using UnityEngine;

/// <summary>
/// Runtime-built impact VFX: particle bursts and camera shake.
/// No scene setup needed - everything is created on first use.
/// </summary>
public class ImpactEffects : MonoBehaviour
{
    private static ImpactEffects _instance;

    private ParticleSystem _particles;
    private ParticleSystemRenderer _renderer;
    private Coroutine _shakeRoutine;
    private Transform _shakenCamera;
    private Vector3 _camBasePos;
    private Transform[] _camChildren = new Transform[0];
    private Vector3[] _camChildBasePos = new Vector3[0];

    public static ImpactEffects Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("ImpactEffects");
                _instance = go.AddComponent<ImpactEffects>();
            }

            return _instance;
        }
    }

    private void Awake()
    {
        BuildParticleSystem();
    }

    private void BuildParticleSystem()
    {
        _particles = gameObject.AddComponent<ParticleSystem>();
        _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = _particles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.035f);
        main.gravityModifier = 0.7f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 256;

        var emission = _particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;

        var shape = _particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.01f;

        var sizeOverLifetime = _particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        _renderer = GetComponent<ParticleSystemRenderer>();

        // An authored material (created by the URP migration) wins; the shader
        // lookup below only covers the built-in pipeline.
        var authored = Resources.Load<Material>(PARTICLE_MATERIAL);
        _renderer.material = authored != null ? authored : new Material(FindParticleShader());
    }

    private const string PARTICLE_MATERIAL = "ImpactParticle";

    private static Shader FindParticleShader()
    {
        var shader = Shader.Find("Legacy Shaders/Particles/Additive");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        return shader;
    }

    /// <summary>Spawns a small burst of colored particles at a world position.</summary>
    public void EmitBurst(Vector3 position, Color color, int count)
    {
        var emitParams = new ParticleSystem.EmitParams
        {
            position = position,
            startColor = color
        };

        _particles.Emit(emitParams, count);
    }

    /// <summary>Brief positional camera shake. Safe to call repeatedly.</summary>
    public void Shake(float intensity = 0.02f, float duration = 0.12f)
    {
        var cam = Camera.main;
        if (cam == null) return;

        if (_shakeRoutine != null)
        {
            StopCoroutine(_shakeRoutine);
            RestoreShakeBase();
        }

        _shakeRoutine = StartCoroutine(ShakeRoutine(cam.transform, intensity, duration));
    }

    private IEnumerator ShakeRoutine(Transform camTransform, float intensity, float duration)
    {
        CaptureShakeBase(camTransform);

        // Smooth Perlin-noise shake with an eased fade-out (per-frame random looks jittery).
        var seedX = Random.value * 100f;
        var seedY = Random.value * 100f;
        const float frequency = 18f;

        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var damper = 1f - Mathf.SmoothStep(0f, 1f, elapsed / duration);
            var x = Mathf.PerlinNoise(seedX, elapsed * frequency) * 2f - 1f;
            var y = Mathf.PerlinNoise(seedY, elapsed * frequency) * 2f - 1f;

            // Along the camera's own right/up so the shake reads on screen;
            // world axes would push it along its view direction instead.
            var worldOffset = (camTransform.right * x + camTransform.up * y) * (intensity * damper);
            camTransform.position = _camBasePos + worldOffset;

            // Children of the camera (the 2D background) would otherwise ride
            // along and stay locked on screen while the table shakes in front of
            // them. Holding them still in the world makes them shake too, a bit
            // less than the table since they are further away.
            var localOffset = camTransform.InverseTransformVector(worldOffset);
            for (var i = 0; i < _camChildren.Length; i++)
            {
                if (_camChildren[i] != null)
                    _camChildren[i].localPosition = _camChildBasePos[i] - localOffset;
            }

            yield return null;
        }

        RestoreShakeBase();
        _shakeRoutine = null;
    }

    private void CaptureShakeBase(Transform camTransform)
    {
        _shakenCamera = camTransform;
        _camBasePos = camTransform.position;

        _camChildren = new Transform[camTransform.childCount];
        _camChildBasePos = new Vector3[camTransform.childCount];
        for (var i = 0; i < camTransform.childCount; i++)
        {
            _camChildren[i] = camTransform.GetChild(i);
            _camChildBasePos[i] = _camChildren[i].localPosition;
        }
    }

    private void RestoreShakeBase()
    {
        if (_shakenCamera == null) return;

        _shakenCamera.position = _camBasePos;
        for (var i = 0; i < _camChildren.Length; i++)
        {
            if (_camChildren[i] != null)
                _camChildren[i].localPosition = _camChildBasePos[i];
        }
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }
}
