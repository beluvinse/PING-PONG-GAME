using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Tools > Ping Pong > Build Lose Particles. Sets up the YOU LOSE tears as a
/// regular ParticleSystem you can design in the Inspector.
///
/// The UI canvas is Screen Space Overlay, which draws over everything a camera
/// renders, so particles in the world would end up hidden behind the end screen.
/// Instead a rig far from the table holds the ParticleSystem and its own camera;
/// that camera renders into a RenderTexture, and a RawImage inside MatchEndScreen,
/// just behind the card, shows it. UIManager switches the rig on only while the
/// screen is up.
/// </summary>
public static class LoseParticlesBuilder
{
    private const string RIG_NAME = "LoseParticlesRig";
    private const string VIEW_NAME = "LoseParticlesView";
    private const string TEXTURE_PATH = "Assets/Settings/LoseParticles.renderTexture";
    private const string MATERIAL_PATH = "Assets/Art/Materials/LoseParticle.mat";
    private const string TEAR_TEXTURE_PATH = "Assets/Art/Textures/tear-capsule.png";
    private const string PLACEHOLDER_TEXTURE_NAME = "Default-Particle";
    private const string PARTICLE_SHADER = "Custom/UnlitParticle";

    // Far below the table, so the game camera never sees the particles directly.
    private static readonly Vector3 RigPosition = new Vector3(0f, -5000f, 0f);

    // World units the particle camera sees from top to bottom; 10.8 keeps 1 unit = 100 canvas pixels at 1080p.
    private const float VIEW_HEIGHT = 10.8f;

    [MenuItem("Tools/Ping Pong/Build Lose Particles")]
    private static void Build()
    {
        var ui = Object.FindObjectOfType<UIManager>(true);
        if (ui == null)
        {
            EditorUtility.DisplayDialog("Lose particles", "No UIManager in the open scene. Open Game.unity and run this again.", "OK");
            return;
        }

        var so = new SerializedObject(ui);
        if (so.FindProperty("_loseParticles") == null)
        {
            EditorUtility.DisplayDialog("Lose particles", "UIManager has no lose particle fields yet. Let Unity finish compiling and run this again.", "OK");
            return;
        }

        var screen = so.FindProperty("_matchEndScreen").objectReferenceValue as GameObject;
        if (screen == null)
        {
            EditorUtility.DisplayDialog("Lose particles", "UIManager has no Match End Screen assigned. Build or assign it first.", "OK");
            return;
        }

        if (Shader.Find(PARTICLE_SHADER) == null)
        {
            EditorUtility.DisplayDialog("Lose particles", $"Shader '{PARTICLE_SHADER}' isn't imported yet. Let Unity finish importing and run this again.", "OK");
            return;
        }

        var scene = ui.gameObject.scene;
        GameObject oldRig = null;
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == RIG_NAME) oldRig = root;
        var oldView = screen.transform.Find(VIEW_NAME);

        if ((oldRig != null || oldView != null) &&
            !EditorUtility.DisplayDialog("Lose particles", "The lose particles already exist. Replace them? Changes made to the old ParticleSystem are lost.", "Replace", "Cancel"))
            return;

        if (oldRig != null) Undo.DestroyObjectImmediate(oldRig);
        if (oldView != null) Undo.DestroyObjectImmediate(oldView.gameObject);

        var texture = LoadOrCreateTexture();
        var material = LoadOrCreateTearMaterial();

        var rig = new GameObject(RIG_NAME);
        if (rig.scene != scene) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(rig, scene);
        rig.transform.position = RigPosition;

        BuildCamera(rig.transform, texture);
        var particles = BuildTears(rig.transform, material);
        var view = BuildView(screen, texture, so.FindProperty("_matchEndCard").objectReferenceValue as RectTransform);

        rig.SetActive(false);                               // UIManager turns it on when the player loses
        Undo.RegisterCreatedObjectUndo(rig, "Build Lose Particles");
        Undo.RegisterCreatedObjectUndo(view.gameObject, "Build Lose Particles");

        so.FindProperty("_loseParticlesRig").objectReferenceValue = rig;
        so.FindProperty("_loseParticles").objectReferenceValue = particles;
        so.FindProperty("_loseParticlesView").objectReferenceValue = view;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = particles.gameObject;

        EditorUtility.DisplayDialog("Lose particles",
            $"Built '{RIG_NAME}' (camera + tears, switched off) and '{VIEW_NAME}' inside MatchEndScreen, and wired them into UIManager.\n\n"
            + $"The tears use {MATERIAL_PATH}. The rig is switched off: turn it on while editing to preview, and back off before saving.", "OK");
    }

    private static RenderTexture LoadOrCreateTexture()
    {
        var texture = AssetDatabase.LoadAssetAtPath<RenderTexture>(TEXTURE_PATH);
        if (texture != null) return texture;

        // 16:9 like the reference resolution, so the particle camera frames the same shape as the screen.
        texture = new RenderTexture(1280, 720, 16, RenderTextureFormat.ARGB32) { name = "LoseParticles" };
        AssetDatabase.CreateAsset(texture, TEXTURE_PATH);
        return texture;
    }

    /// <summary>
    /// Keeps a texture you already put on the material; only a missing texture or
    /// Unity's soft round placeholder is swapped for the crisp tear capsule.
    /// </summary>
    private static Material LoadOrCreateTearMaterial()
    {
        // Custom/UnlitParticle: unlit, alpha blended, tinted by the particle colours, and - unlike
        // Sprites/Default - shows an Image slot in the Inspector.
        var shader = Shader.Find(PARTICLE_SHADER);

        var material = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL_PATH);
        if (material == null)
        {
            material = new Material(shader) { name = "LoseParticle" };
            AssetDatabase.CreateAsset(material, MATERIAL_PATH);
        }
        else if (shader != null && material.shader != shader)
        {
            // An older build used Sprites/Default, whose texture can't be set by hand.
            var keep = material.mainTexture;
            material.shader = shader;
            material.mainTexture = keep;
            EditorUtility.SetDirty(material);
        }

        var current = material.mainTexture;
        var capsule = LoadTearTexture();
        if (capsule != null && (current == null || current.name == PLACEHOLDER_TEXTURE_NAME))
        {
            material.mainTexture = capsule;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
        }
        return material;
    }

    private static Texture2D LoadTearTexture()
    {
        var importer = AssetImporter.GetAtPath(TEAR_TEXTURE_PATH) as TextureImporter;
        if (importer == null) return null;

        // Clamp, or the capsule's round ends pick up pixels from the opposite edge.
        if (importer.wrapMode != TextureWrapMode.Clamp || !importer.alphaIsTransparency)
        {
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(TEAR_TEXTURE_PATH);
    }

    private static void BuildCamera(Transform rig, RenderTexture texture)
    {
        var go = new GameObject("LoseParticlesCamera", typeof(Camera));
        go.transform.SetParent(rig, false);
        go.transform.localPosition = new Vector3(0f, 0f, -10f);

        var camera = go.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = VIEW_HEIGHT / 2f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0f, 0f, 0f, 0f);  // transparent, so only the particles show
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 30f;
        camera.allowHDR = false;
        camera.allowMSAA = false;
        camera.targetTexture = texture;

        // Post-processing would tint the particles twice and can drop the transparent background.
        var data = camera.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = false;
        data.renderShadows = false;
        data.antialiasing = AntialiasingMode.None;
    }

    /// <summary>
    /// Anime tears that drip: each appears short, holds for a beat, stretches
    /// downwards, starts sliding while it stretches a little more, then falls faster
    /// and faster and fades out. The renderer pivot sits on the particle's top edge,
    /// so growing only ever lengthens it downwards, like a running drop.
    /// Every curve here is a starting point to tune in the Inspector.
    /// </summary>
    private static ParticleSystem BuildTears(Transform rig, Material material)
    {
        var go = new GameObject("LoseParticles", typeof(ParticleSystem));
        go.transform.SetParent(rig, false);
        go.transform.localPosition = new Vector3(0f, 1.5f, 0f);

        var tears = go.GetComponent<ParticleSystem>();
        tears.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = tears.main;
        main.duration = 2f;
        main.loop = true;                                    // keeps crying while the screen is up
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.2f);
        main.startSpeed = 0f;                                // all movement comes from Velocity over Lifetime
        main.startRotation = 0f;
        main.startSize3D = true;
        main.startSizeX = 0.3f;                              // width
        main.startSizeY = 1.2f;                              // length at 1 on the size curve (the texture is 1:4)
        main.startSizeZ = 1f;
        main.startColor = new Color(0.97f, 0.98f, 1f, 1f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.useUnscaledTime = true;                         // unaffected by pause
        main.maxParticles = 20;

        var emission = tears.emission;
        emission.rateOverTime = 3f;

        // A wide, shallow band around the middle of the screen.
        var shape = tears.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(12f, 1.5f, 0.01f);

        // Width stays put; length goes short -> hold -> stretch -> a little more -> long.
        var size = tears.sizeOverLifetime;
        size.enabled = true;
        size.separateAxes = true;
        size.x = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Constant(0f, 1f, 1f));
        size.y = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.35f), new Keyframe(0.15f, 0.35f),
            new Keyframe(0.35f, 1f), new Keyframe(0.55f, 1.05f),
            new Keyframe(0.7f, 1.3f), new Keyframe(1f, 1.6f)));
        size.z = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Constant(0f, 1f, 1f));

        // Still while it first stretches, then sliding, then falling faster and faster.
        // All three axes must use the same curve mode.
        var velocity = tears.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(0f, AnimationCurve.Constant(0f, 1f, 0f));
        velocity.y = new ParticleSystem.MinMaxCurve(-6f, new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.3f, 0f),
            new Keyframe(0.55f, 0.1f), new Keyframe(0.75f, 0.25f), new Keyframe(1f, 1f)));
        velocity.z = new ParticleSystem.MinMaxCurve(0f, AnimationCurve.Constant(0f, 1f, 0f));

        // Pops in, stays solid, fades at the end of the fall.
        var color = tears.colorOverLifetime;
        color.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.04f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        color.color = fade;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.pivot = new Vector3(0f, -0.5f, 0f);         // anchored on the top edge: it only grows downwards
        renderer.sortMode = ParticleSystemSortMode.OldestInFront;

        return tears;
    }

    private static RawImage BuildView(GameObject screen, RenderTexture texture, RectTransform card)
    {
        var go = new GameObject(VIEW_NAME, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(screen.transform, false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // Just behind the card: over the background, under the title, scores and buttons.
        if (card != null && card.parent == screen.transform)
            go.transform.SetSiblingIndex(card.GetSiblingIndex());

        var view = go.GetComponent<RawImage>();
        view.texture = texture;
        view.raycastTarget = false;
        view.enabled = false;                                // UIManager shows it when the player loses
        return view;
    }
}
