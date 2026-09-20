using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tools > Ping Pong > Create Sound Bank and Add UI Sounds: the two setup steps
/// for audio.
///
/// The bank is created empty, one row per <see cref="SoundId"/>, with volume,
/// pitch spread and repeat limit already set to something sensible for that kind
/// of sound. Dropping clips in is all that is left. Running it again only adds
/// rows that are missing, so it never touches work already done.
/// </summary>
public static class AudioSetup
{
    private const string BANK_PATH = "Assets/Resources/SoundBank.asset";

    [MenuItem("Tools/Ping Pong/Create Sound Bank")]
    private static void CreateSoundBank()
    {
        const string title = "Sound bank";

        var bank = AssetDatabase.LoadAssetAtPath<SoundBank>(BANK_PATH);
        var created = bank == null;
        if (created)
        {
            // AudioManager loads it by name, so it has to sit in a Resources folder.
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            bank = ScriptableObject.CreateInstance<SoundBank>();
            AssetDatabase.CreateAsset(bank, BANK_PATH);
        }

        var entries = bank.Entries.ToList();
        var known = new HashSet<SoundId>(entries.Where(e => e != null).Select(e => e.id));
        var added = 0;

        foreach (SoundId id in System.Enum.GetValues(typeof(SoundId)))
        {
            if (id == SoundId.None || known.Contains(id)) continue;

            entries.Add(NewEntry(id));
            added++;
        }

        bank.SetEntries(entries.ToArray());
        EditorUtility.SetDirty(bank);
        AssetDatabase.SaveAssets();
        Selection.activeObject = bank;

        EditorUtility.DisplayDialog(title,
            (created ? $"Created {BANK_PATH}" : $"Updated {BANK_PATH}") + $" with {added} new sound(s).\n\n"
            + "Drop your clips on each row. A row with no clips stays silent, so the game can be played "
            + "at any point along the way.\n\n"
            + "Several clips on one row means a different one plays each time, never the same twice in a row.\n\n"
            + "The Music field at the top is the background track: it loops from the moment the game starts and "
            + "carries on from the menu into the match.", "OK");
    }

    /// <summary>
    /// Starting values per kind of sound. Loud, one-off moments keep their exact
    /// pitch; small, repeated ones get a little spread and a repeat limit.
    /// </summary>
    private static SoundBank.Entry NewEntry(SoundId id)
    {
        var entry = new SoundBank.Entry { id = id, volume = 0.9f, pitch = new Vector2(0.97f, 1.03f) };

        switch (id)
        {
            case SoundId.ButtonHover:
                entry.volume = 0.45f;                        // heard constantly: it has to sit under everything
                entry.pitch = new Vector2(0.98f, 1.02f);
                entry.minInterval = 0.05f;
                break;

            case SoundId.SliderTick:
                entry.volume = 0.35f;
                entry.pitch = new Vector2(0.98f, 1.04f);
                entry.minInterval = 0.05f;                   // a dragged slider would fire every frame
                break;

            case SoundId.TableBounce:
                entry.volume = 0.7f;
                entry.pitch = new Vector2(0.96f, 1.08f);
                entry.minInterval = 0.03f;
                break;

            case SoundId.PaddleHit:
                entry.volume = 1f;
                entry.pitch = new Vector2(0.94f, 1.06f);     // the widest spread: it fires the most
                break;

            case SoundId.CountdownTick:
                entry.pitch = Vector2.one;                   // the three ticks have to match each other
                break;

            case SoundId.CountdownGo:
            case SoundId.MatchPoint:
            case SoundId.Victory:
            case SoundId.Defeat:
                entry.volume = 1f;
                entry.pitch = Vector2.one;                   // one-off moments: always exactly the same
                break;
        }

        return entry;
    }

    [MenuItem("Tools/Ping Pong/Add UI Sounds")]
    private static void AddUiSounds()
    {
        const string title = "UI sounds";

        var buttons = Object.FindObjectsOfType<Button>(true);
        var sliders = Object.FindObjectsOfType<Slider>(true);
        var addedButtons = 0;
        var addedSliders = 0;

        foreach (var button in buttons)
        {
            if (button.GetComponent<UISoundTrigger>() != null) continue;

            var trigger = Undo.AddComponent<UISoundTrigger>(button.gameObject);
            var so = new SerializedObject(trigger);
            so.FindProperty("_click").enumValueIndex = ClickIndex(button.name);
            so.ApplyModifiedPropertiesWithoutUndo();
            addedButtons++;
        }

        foreach (var slider in sliders)
        {
            if (slider.GetComponent<UISliderSound>() != null) continue;

            Undo.AddComponent<UISliderSound>(slider.gameObject);
            addedSliders++;
        }

        if (addedButtons + addedSliders > 0) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        EditorUtility.DisplayDialog(title,
            $"Added hover and click sounds to {addedButtons} button(s) and a tick to {addedSliders} slider(s).\n\n"
            + "Buttons named back or apply were given their own sound; that is a guess from the name, so check "
            + "the Click field on each one.\n\n"
            + "Buttons that already had the component were left alone. Run this again after adding new buttons, "
            + "and once per scene.", "OK");
    }

    /// <summary>
    /// Back and Apply are the two buttons with a sound of their own, and their
    /// names are the only clue available here - worth a guess, not a promise.
    /// </summary>
    private static int ClickIndex(string buttonName)
    {
        var name = buttonName.ToLowerInvariant();
        var id = SoundId.ButtonClick;

        if (name.Contains("back") || name.Contains("cancel") || name.Contains("volver")) id = SoundId.ButtonBack;
        else if (name.Contains("apply") || name.Contains("confirm") || name.Contains("aplicar")) id = SoundId.ButtonApply;

        return System.Array.IndexOf(System.Enum.GetValues(typeof(SoundId)), id);
    }
}
