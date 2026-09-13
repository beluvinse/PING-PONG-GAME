using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-click setup for the menu -> game transition: puts both scenes in Build
/// Settings (scene loading fails without that) and adds SceneLoadButton to the
/// play button of whichever scene is open.
/// </summary>
public static class SceneTransitionSetup
{
    private const string MENU_SCENE = "Assets/Scenes/Menu.unity";
    private const string GAME_SCENE = "Assets/Scenes/Game.unity";

    [MenuItem("Tools/Ping Pong/Setup Scene Transition")]
    private static void Setup()
    {
        var log = new List<string>();
        AddScenesToBuild(log);
        WirePlayButton(log);

        Debug.Log("[Scene transition setup]\n- " + string.Join("\n- ", log));
        EditorUtility.DisplayDialog("Scene transition", string.Join("\n", log), "OK");
    }

    private static void AddScenesToBuild(List<string> log)
    {
        // Menu first so a build starts there; keep anything already listed.
        var ordered = new List<string> { MENU_SCENE, GAME_SCENE };
        var scenes = new List<EditorBuildSettingsScene>();

        foreach (var path in ordered)
        {
            if (!File.Exists(path))
            {
                log.Add($"WARNING: {path} not found");
                continue;
            }
            scenes.Add(new EditorBuildSettingsScene(path, true));
        }

        foreach (var existing in EditorBuildSettings.scenes)
        {
            if (ordered.Contains(existing.path)) continue;
            scenes.Add(existing);
        }

        EditorBuildSettings.scenes = scenes.ToArray();
        log.Add("Build Settings: " + string.Join(", ", scenes.Select(s => Path.GetFileNameWithoutExtension(s.path))));
    }

    private static void WirePlayButton(List<string> log)
    {
        var button = Object.FindObjectsOfType<Button>(true)
            .FirstOrDefault(b => b.name.ToLowerInvariant().Contains("play"));

        if (button == null)
        {
            log.Add("No play button in the open scene - open Menu.unity and run this again, "
                    + "or add the SceneLoadButton component to the button by hand.");
            return;
        }

        if (button.GetComponent<SceneLoadButton>() != null)
        {
            log.Add($"{button.name}: already has SceneLoadButton");
            return;
        }

        Undo.AddComponent<SceneLoadButton>(button.gameObject);
        EditorUtility.SetDirty(button.gameObject);
        var scene = button.gameObject.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.Add($"{button.name}: SceneLoadButton added (loads 'Game') and {Path.GetFileName(scene.path)} saved");
    }
}
