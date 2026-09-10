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
    private Vector3 _camBasePos;

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
        _renderer.material = new Material(FindParticleShader());
    }

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
            cam.transform.localPosition = _camBasePos;
        }

        _shakeRoutine = StartCoroutine(ShakeRoutine(cam.transform, intensity, duration));
    }

    private IEnumerator ShakeRoutine(Transform camTransform, float intensity, float duration)
    {
        _camBasePos = camTransform.localPosition;

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
            camTransform.localPosition = _camBasePos + new Vector3(x, y, 0f) * (intensity * damper);
            yield return null;
        }

        camTransform.localPosition = _camBasePos;
        _shakeRoutine = null;
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }
}
