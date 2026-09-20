using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class OptionSwatchGroup : MonoBehaviour
{
    private const string HIGHLIGHT_NAME = "Highlight";

    [Serializable]
    public class Swatch
    {
        public Button button;
        [Tooltip("Enabled while this swatch is the chosen one. Its GameObject stays active.")]
        public Image highlight;
        public Image fill;
    }

    [Tooltip("Every child of this becomes a swatch at runtime, overriding the list below. "
             + "Leave empty to use the list - right-click the component > Fill Swatches From Children fills it.")]
    [SerializeField] private Transform _container;
    [SerializeField] private Swatch[] _swatches;

    public event Action<int> SelectionChanged;

    public int Selected { get; private set; }

    public int Count
    {
        get
        {
            Init();
            return _swatches == null ? 0 : _swatches.Length;
        }
    }

    private bool _initialized;

    private void Awake() => Init();

    /// <summary>
    /// Lazy on purpose. When the options screen switches on, Unity runs Awake and
    /// OnEnable across its objects in no fixed order, so OptionsMenu can ask for
    /// the current selection before this component's own Awake has happened.
    /// </summary>
    private void Init()
    {
        if (_initialized) return;
        _initialized = true;

        if (_container != null)
            _swatches = Collect(_container);

        if (_swatches == null) return;

        for (var i = 0; i < _swatches.Length; i++)
        {
            var index = i;                          // captured per iteration
            if (_swatches[i].button != null)
                _swatches[i].button.onClick.AddListener(() => Select(index));
        }
    }

    /// <summary>
    /// One swatch per child. Handles both layouts in the project:
    /// the generated one, a Button on the child with a "Highlight" child as the
    /// ring; and Swatch.prefab, where the root Image is the ring and the Button
    /// sits on an Image inside it.
    /// </summary>
    private static Swatch[] Collect(Transform container)
    {
        var swatches = new List<Swatch>();

        foreach (Transform child in container)
        {
            var button = child.GetComponentInChildren<Button>(true);
            if (button == null) continue;

            Image highlight = null;
            var named = child.Find(HIGHLIGHT_NAME);
            if (named != null)
                highlight = named.GetComponent<Image>();
            else if (button.transform != child)
                highlight = child.GetComponent<Image>();

            swatches.Add(new Swatch
            {
                button = button,
                highlight = highlight,
                fill = button.GetComponent<Image>()
            });
        }

        return swatches.ToArray();
    }

    /// <summary>The image on that swatch, or null if that slot is empty.</summary>
    public Image FillAt(int index) =>
        index >= 0 && index < Count ? _swatches[index].fill : null;

    /// <summary>Pass notify: false to show a stored choice without firing events.</summary>
    public void Select(int index, bool notify = true)
    {
        if (Count == 0) return;

        Selected = Mathf.Clamp(index, 0, Count - 1);

        // Only the Image component flips; the highlight's GameObject has to stay
        // active in the scene or the ring never shows.
        for (var i = 0; i < _swatches.Length; i++)
            if (_swatches[i].highlight != null)
                _swatches[i].highlight.enabled = i == Selected;

        // Only a real pick makes a sound: notify is false when a stored choice
        // is being shown again as the screen opens.
        if (!notify) return;

        AudioManager.Play(SoundId.AvatarSelect);
        SelectionChanged?.Invoke(Selected);
    }

#if UNITY_EDITOR
    /// <summary>
    /// Right-click the component header > "Fill Swatches From Children". Writes the
    /// list from the Container's children (or this object's, when Container is
    /// empty) using the same rules as the runtime collection. Undoable.
    /// </summary>
    [ContextMenu("Fill Swatches From Children")]
    private void FillSwatchesFromChildren()
    {
        UnityEditor.Undo.RecordObject(this, "Fill Swatches From Children");
        _swatches = Collect(_container != null ? _container : transform);

        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);

        var missingHighlight = 0;
        foreach (var swatch in _swatches)
            if (swatch.highlight == null) missingHighlight++;

        Debug.Log($"{name}: {_swatches.Length} swatches added to the list"
                  + (missingHighlight > 0 ? $", {missingHighlight} without a highlight Image." : "."), this);
    }
#endif

    private void OnDestroy()
    {
        if (_swatches == null) return;

        foreach (var swatch in _swatches)
            if (swatch.button != null)
                swatch.button.onClick.RemoveAllListeners();
    }
}
