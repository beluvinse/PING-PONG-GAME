using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tools > Ping Pong > Add Point Sparkles. A few big stars burst out from behind
/// the score when a point lands.
///
/// The UI canvas is Screen Space Overlay and draws over anything a camera renders,
/// so the particles live in their own rig far from the table: its camera renders
/// into a RenderTexture that a full-screen RawImage shows, on top of the HUD.
/// It cannot go under the scores: that card is opaque, and stars behind it are
/// simply not seen. They are thrown from a ring around the number instead, so they
/// still come out from behind it without covering it. UIManager moves the burst to
/// wherever that number is on screen.
/// </summary>
public static class PointSparklesBuilder
{
    private const string RIG_NAME = "PointSparklesRig";
    private const string VIEW_NAME = "PointSparklesView";
    private const string RENDER_TEXTURE_PATH = "Assets/Settings/PointSparkles.renderTexture";

    // Its own corner of the world: on top of the match-end rig, each camera would
    // film the other one's particles as well.
    private static readonly Vector3 RigPosition = new Vector3(0f, -6000f, 0f);

    [MenuItem("Tools/Ping Pong/Add Point Sparkles")]
    private static void AddPointSparkles()
    {
        const string title = "Point sparkles";
        var ui = Object.FindObjectOfType<UIManager>(true);
        if (ui == null)
        {
            EditorUtility.DisplayDialog(title, "No UIManager in the open scene. Open Game.unity and run this again.", "OK");
            return;
        }

        var so = new SerializedObject(ui);
        if (so.FindProperty("_pointSparkles") == null)
        {
            EditorUtility.DisplayDialog(title, "UIManager has no sparkle fields yet. Let Unity finish compiling and run this again.", "OK");
            return;
        }

        if (Shader.Find(MatchEndParticlesBuilder.PARTICLE_SHADER) == null)
        {
            EditorUtility.DisplayDialog(title, $"Shader '{MatchEndParticlesBuilder.PARTICLE_SHADER}' isn't imported yet. Let Unity finish importing and run this again.", "OK");
            return;
        }

        var scene = ui.gameObject.scene;
        GameObject oldRig = null;
        foreach (var rootObject in scene.GetRootGameObjects())
            if (rootObject.name == RIG_NAME) oldRig = rootObject;

        var hud = ui.transform.Find("GamePanel") != null ? ui.transform.Find("GamePanel") : ui.transform;
        var oldView = hud.Find(VIEW_NAME);

        if ((oldRig != null || oldView != null) &&
            !EditorUtility.DisplayDialog(title, "These sparkles already exist. Replace them? Changes made to the old ParticleSystem are lost.", "Replace", "Cancel"))
            return;

        if (oldRig != null) Undo.DestroyObjectImmediate(oldRig);
        if (oldView != null) Undo.DestroyObjectImmediate(oldView.gameObject);

        var texture = MatchEndParticlesBuilder.LoadOrCreateRenderTexture(RENDER_TEXTURE_PATH, RIG_NAME);
        var material = MatchEndParticlesBuilder.LoadOrCreateMaterial(
            MatchEndParticlesBuilder.STAR_MATERIAL_PATH, "WinParticle",
            MatchEndParticlesBuilder.STAR_TEXTURE_PATH, adjustImport: false);

        var rig = new GameObject(RIG_NAME);
        if (rig.scene != scene) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(rig, scene);
        rig.transform.position = RigPosition;

        MatchEndParticlesBuilder.BuildCamera(rig.transform, texture);
        var sparkles = BuildSparkles(rig.transform, material);
        var view = BuildView(hud, texture);

        rig.SetActive(false);                                // UIManager turns it on for each point
        Undo.RegisterCreatedObjectUndo(rig, "Add Point Sparkles");
        Undo.RegisterCreatedObjectUndo(view.gameObject, "Add Point Sparkles");

        so.FindProperty("_pointSparklesRig").objectReferenceValue = rig;
        so.FindProperty("_pointSparkles").objectReferenceValue = sparkles;
        so.FindProperty("_pointSparklesView").objectReferenceValue = view;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = sparkles.gameObject;

        EditorUtility.DisplayDialog(title,
            $"Built '{RIG_NAME}' (camera + particles, switched off) and '{VIEW_NAME}' on top of the HUD, and wired them into UIManager.\n\n"
            + $"They share the win screen's star material ({MatchEndParticlesBuilder.STAR_MATERIAL_PATH}), so both use the same image.\n\n"
            + "The burst goes off when the new number lands, from behind it.", "OK");
    }

    /// <summary>
    /// Three or four big stars thrown out from behind the number: they grow, spin
    /// slowly, arc outwards and fade. Few and large on purpose - this fires on every
    /// point, so it has to read in a glance without becoming confetti.
    /// </summary>
    private static ParticleSystem BuildSparkles(Transform rig, Material material)
    {
        var go = new GameObject("PointSparkles", typeof(ParticleSystem));
        go.transform.SetParent(rig, false);

        var sparkles = go.GetComponent<ParticleSystem>();
        sparkles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = sparkles.main;
        main.duration = 1f;
        main.loop = false;                                   // one burst per point
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.55f, 0.95f);   // 55 to 95 canvas pixels
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 0.4f;                         // they arc instead of flying straight
        main.startColor = Color.white;                       // the star image brings its own colour
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.useUnscaledTime = true;
        main.maxParticles = 8;

        var emission = sparkles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 4) });

        // A ring the size of the number, emitting on the edge only: they appear
        // around it and fly outwards, never on top of the digit itself.
        var shape = sparkles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.4f;
        shape.radiusThickness = 0f;
        shape.arc = 360f;

        var size = sparkles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0f)));

        var rotation = sparkles.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);

        var color = sparkles.colorOverLifetime;
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

        return sparkles;
    }

    /// <summary>Full-screen RawImage showing what the rig's camera renders.</summary>
    private static RawImage BuildView(Transform hud, RenderTexture texture)
    {
        var go = new GameObject(VIEW_NAME, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(hud, false);
        go.transform.SetAsLastSibling();                     // over the HUD, under the pause and end screens

        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var view = go.GetComponent<RawImage>();
        view.texture = texture;
        view.raycastTarget = false;
        view.enabled = false;                                // UIManager shows it while a burst runs
        return view;
    }

}
