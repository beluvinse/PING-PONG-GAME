using UnityEngine;

/// <summary>
/// Gives the whole scene a soft, warm cartoon look at startup:
/// 1. Swaps every Standard-shader material to the Custom/Toon shader,
///    keeping each material's color and texture (shared property names).
/// 2. Warms up the directional light and raises the ambient so nothing goes dark.
/// Bootstraps itself - no scene setup needed. Trails and particles are left untouched.
/// All changes are runtime-only (material instances, lighting restored on destroy),
/// so nothing on disk is modified.
/// </summary>
public class ToonLook : MonoBehaviour
{
    private const string TOON_SHADER_NAME = "Custom/Toon";
    private const string STANDARD_SHADER_NAME = "Standard";

    [Header("Scene mood (warm afternoon)")]
    [SerializeField] private Color _sunColor = new Color(1f, 0.93f, 0.82f);
    [SerializeField] private float _sunIntensity = 1.15f;
    [SerializeField] private Color _ambientColor = new Color(0.55f, 0.5f, 0.47f);

    private Light _sun;
    private Color _originalSunColor;
    private float _originalSunIntensity;
    private UnityEngine.Rendering.AmbientMode _originalAmbientMode;
    private Color _originalAmbientColor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        new GameObject("ToonLook").AddComponent<ToonLook>();
    }

    private void Start()
    {
        ApplyToonShader();
        WarmUpLighting();
    }

    private void ApplyToonShader()
    {
        var toonShader = Shader.Find(TOON_SHADER_NAME);
        if (toonShader == null)
        {
            Debug.LogWarning($"ToonLook: shader '{TOON_SHADER_NAME}' not found, keeping Standard look.");
            return;
        }

        foreach (var renderer in FindObjectsOfType<Renderer>(true))
        {
            if (renderer is TrailRenderer || renderer is ParticleSystemRenderer || renderer is LineRenderer)
                continue;

            // renderer.materials returns per-renderer instances, so shared assets
            // (and FBX-embedded materials) are never modified on disk.
            foreach (var material in renderer.materials)
            {
                if (material != null && material.shader != null && material.shader.name == STANDARD_SHADER_NAME)
                    material.shader = toonShader;
            }
        }
    }

    private void WarmUpLighting()
    {
        foreach (var light in FindObjectsOfType<Light>())
        {
            if (light.type != LightType.Directional) continue;
            _sun = light;
            break;
        }

        if (_sun != null)
        {
            _originalSunColor = _sun.color;
            _originalSunIntensity = _sun.intensity;
            _sun.color = _sunColor;
            _sun.intensity = _sunIntensity;
        }

        _originalAmbientMode = RenderSettings.ambientMode;
        _originalAmbientColor = RenderSettings.ambientLight;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = _ambientColor;
    }

    private void OnDestroy()
    {
        // Lighting settings can leak out of play mode in the editor - restore them.
        if (_sun != null)
        {
            _sun.color = _originalSunColor;
            _sun.intensity = _originalSunIntensity;
        }

        RenderSettings.ambientMode = _originalAmbientMode;
        RenderSettings.ambientLight = _originalAmbientColor;
    }
}
