using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Tools > Ping Pong > Build Win / Lose Particles. Sets up both end-screen effects
/// as regular ParticleSystems you can design in the Inspector.
///
/// The UI canvas is Screen Space Overlay, which draws over everything a camera
/// renders, so particles in the world would end up hidden behind the end screen.
/// Instead each rig, far from the table, holds its ParticleSystem and its own
/// camera; that camera renders into a RenderTexture, and a RawImage inside
/// MatchEndScreen, just behind the card, shows it. UIManager switches a rig on
/// only while its screen is up.
/// </summary>
public static class MatchEndParticlesBuilder
{
    private enum Kind { Win, Lose }

    internal const string PARTICLE_SHADER = "Custom/UnlitParticle";
    internal const string STAR_TEXTURE_PATH = "Assets/Art/Icons/NEW UI/star.png";
    internal const string STAR_MATERIAL_PATH = "Assets/Art/Materials/WinParticle.mat";
    private const string BALL_TEXTURE_PATH = "Assets/Art/Textures/tear-ball.png";
    private const string TRAIL_TEXTURE_PATH = "Assets/Art/Textures/tear-trail.png";

    // Textures an earlier build put on the materials; anything else is yours and is kept.
    private static readonly string[] GeneratedTextureNames =
        { "Default-Particle", "tear-capsule", "tear-ball", "tear-trail", "star" };

    // Far below the table, so the game camera never sees the particles directly.
    private static readonly Vector3 RigPosition = new Vector3(0f, -5000f, 0f);

    // World units the particle camera sees from top to bottom; 10.8 keeps 1 unit = 100 canvas pixels at 1080p.
    private const float VIEW_HEIGHT = 10.8f;

    [MenuItem("Tools/Ping Pong/Build Win Particles")]
    private static void BuildWin() => Build(Kind.Win);

    [MenuItem("Tools/Ping Pong/Build Lose Particles")]
    private static void BuildLose() => Build(Kind.Lose);

    private static void Build(Kind kind)
    {
        var title = kind == Kind.Win ? "Win particles" : "Lose particles";
        var rigName = kind == Kind.Win ? "WinParticlesRig" : "LoseParticlesRig";
        var viewName = kind == Kind.Win ? "WinParticlesView" : "LoseParticlesView";
        var fieldPrefix = kind == Kind.Win ? "_winParticles" : "_loseParticles";
        var renderTexturePath = kind == Kind.Win ? "Assets/Settings/WinParticles.renderTexture" : "Assets/Settings/LoseParticles.renderTexture";
        var materialPath = kind == Kind.Win ? STAR_MATERIAL_PATH : "Assets/Art/Materials/LoseParticle.mat";

        var ui = Object.FindObjectOfType<UIManager>(true);
        if (ui == null)
        {
            EditorUtility.DisplayDialog(title, "No UIManager in the open scene. Open Game.unity and run this again.", "OK");
            return;
        }

        var so = new SerializedObject(ui);
        if (so.FindProperty(fieldPrefix) == null)
        {
            EditorUtility.DisplayDialog(title, $"UIManager has no '{fieldPrefix}' field yet. Let Unity finish compiling and run this again.", "OK");
            return;
        }

        var screen = so.FindProperty("_matchEndScreen").objectReferenceValue as GameObject;
        if (screen == null)
        {
            EditorUtility.DisplayDialog(title, "UIManager has no Match End Screen assigned. Build or assign it first.", "OK");
            return;
        }

        if (Shader.Find(PARTICLE_SHADER) == null)
        {
            EditorUtility.DisplayDialog(title, $"Shader '{PARTICLE_SHADER}' isn't imported yet. Let Unity finish importing and run this again.", "OK");
            return;
        }

        var scene = ui.gameObject.scene;
        GameObject oldRig = null;
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == rigName) oldRig = root;
        var oldView = screen.transform.Find(viewName);

        if ((oldRig != null || oldView != null) &&
            !EditorUtility.DisplayDialog(title, "These particles already exist. Replace them? Changes made to the old ParticleSystem are lost.", "Replace", "Cancel"))
            return;

        if (oldRig != null) Undo.DestroyObjectImmediate(oldRig);
        if (oldView != null) Undo.DestroyObjectImmediate(oldView.gameObject);

        var renderTexture = LoadOrCreateRenderTexture(renderTexturePath, rigName);
        var material = LoadOrCreateMaterial(materialPath, System.IO.Path.GetFileNameWithoutExtension(materialPath),
                                            kind == Kind.Win ? STAR_TEXTURE_PATH : BALL_TEXTURE_PATH,
                                            adjustImport: kind == Kind.Lose);

        var rig = new GameObject(rigName);
        if (rig.scene != scene) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(rig, scene);
        rig.transform.position = RigPosition;

        BuildCamera(rig.transform, renderTexture);
        var particles = kind == Kind.Win
            ? BuildStars(rig.transform, material)
            : BuildTears(rig.transform, material, LoadOrCreateMaterial("Assets/Art/Materials/LoseParticleTrail.mat", "LoseParticleTrail", TRAIL_TEXTURE_PATH, adjustImport: true));
        var view = BuildView(screen, viewName, renderTexture, so.FindProperty("_matchEndCard").objectReferenceValue as RectTransform);

        rig.SetActive(false);                               // UIManager turns it on when that screen shows
        Undo.RegisterCreatedObjectUndo(rig, "Build Match End Particles");
        Undo.RegisterCreatedObjectUndo(view.gameObject, "Build Match End Particles");

        so.FindProperty(fieldPrefix + "Rig").objectReferenceValue = rig;
        so.FindProperty(fieldPrefix).objectReferenceValue = particles;
        so.FindProperty(fieldPrefix + "View").objectReferenceValue = view;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = particles.gameObject;

        EditorUtility.DisplayDialog(title,
            $"Built '{rigName}' (camera + particles, switched off) and '{viewName}' inside MatchEndScreen, and wired them into UIManager.\n\n"
            + $"Image: {materialPath}.\n"
            + (kind == Kind.Win ? "WinParticles is the burst; its child WinTwinkle is the background sparkle, played and stopped along with it. " : string.Empty)
            + "The rig is switched off: turn it on while editing to preview, and back off before saving.", "OK");
    }

    internal static RenderTexture LoadOrCreateRenderTexture(string path, string name)
    {
        var texture = AssetDatabase.LoadAssetAtPath<RenderTexture>(path);
        if (texture != null) return texture;

        // 16:9 like the reference resolution, so the particle camera frames the same shape as the screen.
        texture = new RenderTexture(1280, 720, 16, RenderTextureFormat.ARGB32) { name = name };
        AssetDatabase.CreateAsset(texture, path);
        return texture;
    }

    /// <summary>
    /// Custom/UnlitParticle: unlit, alpha blended, tinted by the particle colours,
    /// and - unlike Sprites/Default - shows an Image slot in the Inspector.
    /// A texture you put on the material yourself is kept; only an empty slot or one
    /// of the default textures is replaced.
    /// </summary>
    internal static Material LoadOrCreateMaterial(string path, string name, string texturePath, bool adjustImport)
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
        var isDefault = current == null || System.Array.IndexOf(GeneratedTextureNames, current.name) >= 0;
        var texture = LoadTexture(texturePath, adjustImport);
        if (isDefault && texture != null) material.mainTexture = texture;

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
        return material;
    }

    /// <summary>Generated textures get clamped; your own art is left exactly as you imported it.</summary>
    private static Texture2D LoadTexture(string path, bool adjustImport)
    {
        if (adjustImport && AssetImporter.GetAtPath(path) is TextureImporter importer &&
            (importer.wrapMode != TextureWrapMode.Clamp || !importer.alphaIsTransparency))
        {
            // Clamp, or round edges pick up pixels from the opposite side of the image.
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    internal static void BuildCamera(Transform rig, RenderTexture texture)
    {
        var go = new GameObject("ParticlesCamera", typeof(Camera));
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
    /// Win: one burst of stars thrown out from the middle of the screen. They spin,
    /// fall under gravity like confetti, grow from nothing to full size and shrink
    /// away again, and fade at the end. BuildTwinkle hangs the background stars off
    /// this system, so both play and stop as one.
    /// Every value here is a starting point to tune in the Inspector.
    /// </summary>
    private static ParticleSystem BuildStars(Transform rig, Material material)
    {
        var go = new GameObject("WinParticles", typeof(ParticleSystem));
        go.transform.SetParent(rig, false);

        var stars = go.GetComponent<ParticleSystem>();
        stars.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = stars.main;
        main.duration = 2f;
        main.loop = false;                                   // a single burst, replayed each time you win
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 10f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = new ParticleSystem.MinMaxCurve(0.8f, 1.2f);
        main.startColor = Color.white;                       // the star sprite brings its own colour
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.useUnscaledTime = true;                         // unaffected by pause
        main.maxParticles = 40;

        // Nothing over time: everything leaves at once, like a party popper.
        var emission = stars.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 24) });

        // Out from the centre, in every direction on screen.
        var shape = stars.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.05f;
        shape.radiusThickness = 1f;
        shape.arc = 360f;

        // Small -> big -> small.
        var size = stars.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.35f, 1f), new Keyframe(1f, 0f)));

        var rotation = stars.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);   // radians per second

        var color = stars.colorOverLifetime;
        color.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        color.color = fade;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortMode = ParticleSystemSortMode.OldestInFront;

        BuildTwinkle(go.transform, material);
        return stars;
    }

    /// <summary>
    /// The quiet half of the win effect: small stars popping in and out all over
    /// the screen, so it keeps sparkling after the confetti has landed. It is a
    /// child of the burst, so the Play / Stop UIManager already does (children
    /// included) drives both, and it sits further from the camera to stay behind.
    /// </summary>
    private static void BuildTwinkle(Transform burst, Material material)
    {
        var go = new GameObject("WinTwinkle", typeof(ParticleSystem));
        go.transform.SetParent(burst, false);
        go.transform.localPosition = new Vector3(0f, 0f, 3f);    // further from the camera: draws behind the burst

        var twinkle = go.GetComponent<ParticleSystem>();
        twinkle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = twinkle.main;
        main.duration = 3f;
        main.loop = true;                                    // keeps twinkling for as long as the screen is up
        main.prewarm = true;                                 // already full of stars on the first frame
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
        main.startSpeed = 0f;                                // they stay put and blink
        main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.34f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 0f;
        main.startColor = new Color(1f, 1f, 1f, 0.75f);      // softer than the burst, it is the background
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.useUnscaledTime = true;
        main.maxParticles = 80;

        var emission = twinkle.emission;
        emission.rateOverTime = 16f;

        // Spread over the whole view.
        var shape = twinkle.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(VIEW_HEIGHT * 16f / 9f, VIEW_HEIGHT, 0.01f);

        // Swelling in and shrinking away is what makes it read as a twinkle.
        var size = twinkle.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0f)));

        var rotation = twinkle.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);

        var color = twinkle.colorOverLifetime;
        color.enabled = true;
        var blink = new Gradient();
        blink.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.35f), new GradientAlphaKey(1f, 0.65f), new GradientAlphaKey(0f, 1f) });
        color.color = blink;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortMode = ParticleSystemSortMode.None;
    }

    /// <summary>
    /// Lose: anime tears. Each particle is a small ball and its trail is the tear
    /// line above it, so the ball is always at the bottom of the line. The ball
    /// barely moves at first, so the line starts short with its top still; then it
    /// slides, then falls faster and faster, the line growing with the speed.
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
        main.useUnscaledTime = true;
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
        trails.ratio = 1f;
        trails.lifetime = 0.2f;
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

    private static RawImage BuildView(GameObject screen, string name, RenderTexture texture, RectTransform card)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
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
        view.enabled = false;                                // UIManager shows it when that screen appears
        return view;
    }
}
