using System;
using UnityEngine;
using UnityEngine.UI;

public class MenuNavigator : MonoBehaviour
{
    private const string MENU_SCREEN_NAME = "MenuScreen";
    private const string OPTIONS_SCREEN_NAME = "OptionsScreen";
    private static readonly string[] OptionsButtonWords = { "Settings", "Options" };

    [Tooltip("Hidden while the options are open.")]
    [SerializeField] private GameObject _menuScreen;
    [SerializeField] private OptionsMenu _optionsMenu;
    [Tooltip("The button on MenuScreen that opens the options.")]
    [SerializeField] private Button _optionsButton;

    private void Awake()
    {
        if (_optionsButton != null)
        {
            _optionsButton.onClick.AddListener(ShowOptions);
        }

        if (_optionsMenu != null) _optionsMenu.Closed += ShowMenu;
    }

    public void ShowOptions()
    {
        if (_menuScreen != null) _menuScreen.SetActive(false);
        if (_optionsMenu != null) _optionsMenu.Open();
    }

    public void ShowMenu()
    {
        if (_menuScreen != null) _menuScreen.SetActive(true);
    }

    private void OnDestroy()
    {
        if (_optionsButton != null) _optionsButton.onClick.RemoveListener(ShowOptions);
        if (_optionsMenu != null) _optionsMenu.Closed -= ShowMenu;
    }

#if UNITY_EDITOR
    // Runs when the component is added, so it arrives already wired.
    private void Reset() => AutoAssign();

    /// <summary>
    /// Right-click the component header > "Auto Assign From Scene". Finds
    /// MenuScreen, OptionsScreen and the MenuScreen button whose name contains
    /// "Settings" or "Options". Undoable.
    /// </summary>
    [ContextMenu("Auto Assign From Scene")]
    private void AutoAssign()
    {
        UnityEditor.Undo.RecordObject(this, "Auto Assign Menu Navigator");

        foreach (var t in FindObjectsOfType<Transform>(true))
        {
            if (t.gameObject.scene != gameObject.scene) continue;
            if (t.name == MENU_SCREEN_NAME) _menuScreen = t.gameObject;
            if (t.name == OPTIONS_SCREEN_NAME) _optionsMenu = t.GetComponent<OptionsMenu>();
        }

        _optionsButton = null;
        if (_menuScreen != null)
            foreach (var button in _menuScreen.GetComponentsInChildren<Button>(true))
                if (Array.Exists(OptionsButtonWords, w => button.name.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    _optionsButton = button;
                    break;
                }

        UnityEditor.EditorUtility.SetDirty(this);
        if (gameObject.scene.IsValid())
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);

        Debug.Log($"MenuNavigator: menu screen = {Describe(_menuScreen)}, options menu = {Describe(_optionsMenu)}, "
                  + $"options button = {Describe(_optionsButton)}.", this);

        if (_optionsButton != null && _optionsButton.GetComponent<SceneLoadButton>() != null)
            Debug.LogWarning($"MenuNavigator: '{_optionsButton.name}' has a SceneLoadButton - remove it, "
                             + "or pressing it loads a scene instead of opening the options.", _optionsButton);
    }

    private static string Describe(UnityEngine.Object o) => o != null ? $"'{o.name}'" : "NOT FOUND";
#endif
}
