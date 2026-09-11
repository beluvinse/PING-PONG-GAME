using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// One-shot migration from the built-in render pipeline to URP.
/// Run it once from Tools > Ping Pong > Migrate To URP. Everything it creates is a
/// regular asset (pipeline asset, materials, volume profile), so this file can be
/// deleted afterwards. Running it twice is safe: finished steps are skipped.
/// </summary>
public static class URPMigration
{
    private const string SETTINGS_FOLDER = "Assets/Settings";
    private const string TOON_FOLDER = "Assets/Art/Materials/Toon";
    private const string TOON_GRAPH = "Assets/Shaders/Toon.shadergraph";
    private const string BALL_PREFAB = "Assets/Prefabs/Ball.prefab";
    private const string PARTICLE_MATERIAL = "Assets/Resources/ImpactParticle.mat";

    // BaseShaderGUI.SurfaceType / BlendMode values.
    private const float SURFACE_TRANSPARENT = 1f;
    private const float BLEND_ALPHA = 0f;
    private const float BLEND_ADDITIVE = 2f;

    private static readonly List<string> Log = new List<string>();

    [MenuItem("Tools/Ping Pong/Migrate To URP")]
    private static void Migrate()
    {
        // Scene edits made in Play mode are thrown away on exit, and saving fails.
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("URP migration", "Exit Play mode first, then run it again.", "OK");
            return;
        }

        Log.Clear();

        var toon = AssetDatabase.LoadAssetAtPath<Shader>(TOON_GRAPH);
        var unlit = Shader.Find("Universal Render Pipeline/Unlit");
        var particles = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (toon == null || unlit == null || particles == null)
        {
            EditorUtility.DisplayDialog("URP migration",
                "Missing shaders:\n" +
                (toon == null ? $"- {TOON_GRAPH} (did the graph import?)\n" : "") +
                (unlit == null ? "- Universal Render Pipeline/Unlit\n" : "") +
                (particles == null ? "- Universal Render Pipeline/Particles/Unlit\n" : "") +
                "\nNothing was changed.", "OK");
            return;
        }

        SetupPipeline();
        FixBallPrefab(toon);
        ConvertSceneMaterials(toon, unlit, particles);
        CreateParticleMaterial(particles);
        SetupPostProcessing();
        ReportUnsupportedRenderers();

        AssetDatabase.SaveAssets();
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[URP migration] done\n- " + string.Join("\n- ", Log));
        EditorUtility.DisplayDialog("URP migration", "Done. The full list of changes is in the Console.", "OK");
    }

    private static void SetupPipeline()
    {
        if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset current)
        {
            Log.Add($"Pipeline: already using {current.name}");
            return;
        }

        EnsureFolder(SETTINGS_FOLDER);

        // Mirrors Assets > Create > Rendering > URP Asset (with Universal Renderer).
        // Without postProcessData the renderer silently drops all post-processing.
        var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
        renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
            UniversalRenderPipelineAsset.packagePath + "/Runtime/Data/PostProcessData.asset");
        AssetDatabase.CreateAsset(renderer, SETTINGS_FOLDER + "/PingPong_URP_Renderer.asset");
        ResourceReloader.ReloadAllNullIn(renderer, UniversalRenderPipelineAsset.packagePath);

        var pipeline = UniversalRenderPipelineAsset.Create(renderer);
        AssetDatabase.CreateAsset(pipeline, SETTINGS_FOLDER + "/PingPong_URP.asset");

        GraphicsSettings.defaultRenderPipeline = pipeline;
        Log.Add("Pipeline: created Assets/Settings/PingPong_URP.asset and set it in Graphics settings");

        // A quality level with its own pipeline asset would ignore the default.
        for (var i = 0; i < QualitySettings.names.Length; i++)
        {
            var levelPipeline = QualitySettings.GetRenderPipelineAssetAt(i);
            if (levelPipeline != null && levelPipeline != pipeline)
                Log.Add($"WARNING: quality level '{QualitySettings.names[i]}' overrides the pipeline with {levelPipeline.name}");
        }
    }

    private static void FixBallPrefab(Shader toon)
    {
        // The ball mesh uses Unity's built-in Default-Material (Standard), which URP
        // draws magenta. It is not an asset we can convert, so give it its own.
        var root = PrefabUtility.LoadPrefabContents(BALL_PREFAB);
        try
        {
            var changed = false;
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                {
                    if (!IsBuiltInOnly(materials[i]) || IsProjectAsset(materials[i])) continue;
                    materials[i] = GetOrCreateToonMaterial(toon, "Toon_Ball", null, Color.white);
                    changed = true;
                }
                renderer.sharedMaterials = materials;
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, BALL_PREFAB);
                Log.Add("Ball prefab: Default-Material -> Toon_Ball");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConvertSceneMaterials(Shader toon, Shader unlit, Shader particles)
    {
        var mainCamera = Camera.main;
        var converted = new HashSet<Material>();

        foreach (var renderer in Object.FindObjectsOfType<Renderer>(true))
        {
            var materials = renderer.sharedMaterials;
            var reassigned = false;

            for (var i = 0; i < materials.Length; i++)
            {
                var material = materials[i];
                if (material == null || converted.Contains(material)) continue;

                // Table and blue paddle share a material embedded in the FBX, which
                // can't be edited in place - swap in a toon one using its texture.
                if (AssetDatabase.GetAssetPath(material).EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                {
                    var texture = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
                    var name = "Toon_" + (texture != null ? "Atlas" : material.name.Replace(" ", ""));
                    materials[i] = GetOrCreateToonMaterial(toon, name, texture, Color.white);
                    reassigned = true;
                    Log.Add($"{renderer.name}: FBX material '{material.name}' -> {name}");
                    continue;
                }

                if (!IsBuiltInOnly(material) || !IsProjectAsset(material)) continue;

                var wasTransparent = material.HasProperty("_Mode") && material.GetFloat("_Mode") >= 2f;

                if (renderer is TrailRenderer || renderer is ParticleSystemRenderer || renderer is LineRenderer)
                    Convert(material, particles, wasTransparent ? BLEND_ALPHA : (float?)null);
                else if (mainCamera != null && renderer.transform.IsChildOf(mainCamera.transform))
                    Convert(material, unlit, null);       // the 2D backdrop shows the image as painted
                else
                    Convert(material, toon, wasTransparent ? BLEND_ALPHA : (float?)null);

                converted.Add(material);
                Log.Add($"{renderer.name}: '{material.name}' -> {material.shader.name}{(wasTransparent ? " (transparent)" : "")}");
            }

            if (reassigned)
            {
                Undo.RecordObject(renderer, "URP migration");
                renderer.sharedMaterials = materials;
                EditorUtility.SetDirty(renderer);
            }
        }
    }

    private static void CreateParticleMaterial(Shader particles)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(PARTICLE_MATERIAL);
        if (material == null)
        {
            EnsureFolder(Path.GetDirectoryName(PARTICLE_MATERIAL).Replace('\\', '/'));
            material = new Material(particles);
            AssetDatabase.CreateAsset(material, PARTICLE_MATERIAL);
        }

        material.shader = particles;
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Surface", SURFACE_TRANSPARENT);
        material.SetFloat("_Blend", BLEND_ADDITIVE);
        BaseShaderGUI.SetMaterialKeywords(material);
        EditorUtility.SetDirty(material);
        Log.Add("Impact particles: " + PARTICLE_MATERIAL + " (additive)");
    }

    private static void SetupPostProcessing()
    {
        EnsureFolder(SETTINGS_FOLDER);
        const string profilePath = SETTINGS_FOLDER + "/PingPong_PostFX.asset";

        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
        }

        if (!profile.TryGet(out ColorAdjustments color))
        {
            color = profile.Add<ColorAdjustments>();
            AssetDatabase.AddObjectToAsset(color, profile);
        }

        // What the old hand-written toon shader did in-shader: +15% saturation and
        // a slight warm tint over the whole image.
        color.saturation.Override(15f);
        color.colorFilter.Override(new Color(1.04f, 1f, 0.94f));
        EditorUtility.SetDirty(color);
        EditorUtility.SetDirty(profile);

        var volume = Object.FindObjectOfType<Volume>(true);
        if (volume == null)
        {
            var go = new GameObject("Global Volume");
            Undo.RegisterCreatedObjectUndo(go, "URP migration");
            volume = go.AddComponent<Volume>();
        }
        volume.isGlobal = true;
        volume.sharedProfile = profile;
        EditorUtility.SetDirty(volume);
        Log.Add("Post-processing: Global Volume with " + profilePath + " (Color Adjustments)");

        var mainCamera = Camera.main;
        if (mainCamera != null)
        {
            var cameraData = mainCamera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            EditorUtility.SetDirty(cameraData);
            Log.Add("Camera: post-processing enabled on " + mainCamera.name);
        }
        else
        {
            Log.Add("WARNING: no camera tagged MainCamera - enable Post Processing on the camera by hand");
        }
    }

    private static void ReportUnsupportedRenderers()
    {
        foreach (var renderer in Object.FindObjectsOfType<Renderer>(true))
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            foreach (var material in renderer.sharedMaterials)
            {
                if (IsBuiltInOnly(material))
                    Log.Add($"WARNING: {renderer.name} still uses '{material.name}' ({material.shader.name}) - it will render magenta");
            }
        }
    }

    private static void Convert(Material material, Shader shader, float? transparentBlend)
    {
        // Read before swapping: the built-in names don't exist on URP shaders.
        var color = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
        var texture = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;

        Undo.RecordObject(material, "URP migration");
        material.shader = shader;
        material.shaderKeywords = new string[0];     // drop Standard-only keywords
        material.SetColor("_BaseColor", color);
        material.SetTexture("_BaseMap", texture);
        if (transparentBlend.HasValue)
        {
            material.SetFloat("_Surface", SURFACE_TRANSPARENT);
            material.SetFloat("_Blend", transparentBlend.Value);
        }
        BaseShaderGUI.SetMaterialKeywords(material);   // blend states, render queue, keywords
        EditorUtility.SetDirty(material);
    }

    private static Material GetOrCreateToonMaterial(Shader toon, string name, Texture texture, Color color)
    {
        EnsureFolder(TOON_FOLDER);
        var path = $"{TOON_FOLDER}/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(toon);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = toon;
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", color);
        BaseShaderGUI.SetMaterialKeywords(material);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static bool IsBuiltInOnly(Material material)
    {
        if (material == null || material.shader == null) return false;
        var shader = material.shader.name;
        return shader == "Standard" || shader == "Standard (Specular setup)" ||
               shader.StartsWith("Legacy Shaders/") || shader == "Custom/Toon";
    }

    private static bool IsProjectAsset(Object asset)
    {
        return AssetDatabase.GetAssetPath(asset).StartsWith("Assets/");
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        var parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
