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
    private const string BALL_MATERIAL_PATH = "Assets/Art/Materials/LoseParticle.mat";
    private const string TRAIL_MATERIAL_PATH = "Assets/Art/Materials/LoseParticleTrail.mat";
    private const string BALL_TEXTURE_PATH = "Assets/Art/Textures/tear-ball.png";
    private const string TRAIL_TEXTURE_PATH = "Assets/Art/Textures/tear-trail.png";
    private const string PARTICLE_SHADER = "Custom/UnlitParticle";

    // Textures an earlier build put on the materials; anything else is yours and is kept.
    private static readonly string[] GeneratedTextureNames = { "Default-Particle", "tear-capsule", "tear-ball", "tear-trail" };

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
        var ballMaterial = LoadOrCreateMaterial(BALL_MATERIAL_PATH, "LoseParticle", BALL_TEXTURE_PATH);
        var trailMaterial = LoadOrCreateMaterial(TRAIL_MATERIAL_PATH, "LoseParticleTrail", TRAIL_TEXTURE_PATH);

        var rig = new GameObject(RIG_NAME);
        if (rig.scene != scene) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(rig, scene);
        rig.transform.position = RigPosition;

        BuildCamera(rig.transform, texture);
        var particles = BuildTears(rig.transform, ballMaterial, trailMaterial);
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
            + $"Ball image: {BALL_MATERIAL_PATH}. Line image: {TRAIL_MATERIAL_PATH}.\n"
            + "The rig is switched off: turn it on while editing to preview, and back off before saving.", "OK");
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
    /// Custom/UnlitParticle: unlit, alpha blended, tinted by the particle colours,
    /// and - unlike Sprites/Default - shows an Image slot in the Inspector.
    /// A texture you put on the material yourself is kept; only an empty slot or
    /// one of the generated textures is set to <paramref name="texturePath"/>.
    /// </summary>
    private static Material LoadOrCreateMaterial(string path, string name, string texturePath)
    {
        var shader = Shader.Find(PARTICLE_SHADER);
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        else if (material.shader != shader)
        {
            var keep = material.mainTexture;
            material.shader = shader;
            material.mainTexture = keep;
        }

        var current = material.mainTexture;
        var generated = current == null || System.Array.IndexOf(GeneratedTextureNames, current.name) >= 0;
        var texture = LoadClampedTexture(texturePath);
        if (generated && texture != null) material.mainTexture = texture;

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
        return material;
    }

    private static Texture2D LoadClampedTexture(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return null;

        // Clamp, or round edges pick up pixels from the opposite side of the image.
        if (importer.wrapMode != TextureWrapMode.Clamp || !importer.alphaIsTransparency)
        {
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
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
    /// Anime tears: each particle is a small ball, and its trail is the tear line
    /// above it, so the ball is always at the bottom of the line. The ball barely
    /// moves at first, so the line starts short with its top still; then it slides,
    /// then falls faster and faster, the line growing with the speed, and both fade out.
    /// Every curve here is a starting point to tune in the Inspector.
    /// </summary>
    private static ParticleSystem BuildTears(Transform rig, Material ballMaterial, Material trailMaterial)
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
        main.startSize = 0.22f;                              // ball diameter (22 canvas pixels)
        main.startColor = new Color(0.62f, 0.68f, 1f, 1f);   // soft periwinkle, like the reference
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

        // Creeping, then sliding, then falling faster and faster. The trail only
        // grows while the ball moves, so this curve also shapes the line.
        // All three axes must use the same curve mode.
        var velocity = tears.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(0f, AnimationCurve.Constant(0f, 1f, 0f));
        velocity.y = new ParticleSystem.MinMaxCurve(-6f, new AnimationCurve(
            new Keyframe(0f, 0.05f), new Keyframe(0.15f, 0.05f),
            new Keyframe(0.35f, 0.15f), new Keyframe(0.55f, 0.2f),
            new Keyframe(0.75f, 0.45f), new Keyframe(1f, 1f)));
        velocity.z = new ParticleSystem.MinMaxCurve(0f, AnimationCurve.Constant(0f, 1f, 0f));

        // Pops in, stays solid, fades at the end of the fall. The trail inherits it.
        var color = tears.colorOverLifetime;
        color.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.05f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        color.color = fade;

        // The tear line: where the ball was over the last 20% of its life.
        var trails = tears.trails;
        trails.enabled = true;
        trails.mode = ParticleSystemTrailMode.PerParticle;
        trails.ratio = 1f;                                   // every ball gets a line
        trails.lifetime = 0.2f;                              // longer = longer lines
        trails.minVertexDistance = 0.02f;
        trails.textureMode = ParticleSystemTrailTextureMode.Stretch;
        trails.sizeAffectsWidth = false;
        trails.widthOverTrail = 0.09f;                       // thinner than the ball
        trails.inheritParticleColor = true;
        trails.dieWithParticles = true;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = ballMaterial;
        renderer.trailMaterial = trailMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
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
