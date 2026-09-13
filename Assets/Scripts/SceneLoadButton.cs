using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drop this on a UI button to send it to another scene with the circular wipe.
/// It hooks itself up to the button, and <see cref="Load"/> can also be wired
/// from the button's On Click list.
/// </summary>
public class SceneLoadButton : MonoBehaviour
{
    [SerializeField] private string _sceneName = "Game";

    [Header("Transition (seconds)")]
    [Tooltip("Black circle closing in over the current scene.")]
    [SerializeField] private float _closeDuration = 0.45f;
    [Tooltip("Time held on the black screen. The scene loads during it.")]
    [SerializeField] private float _blackHold = 1f;
    [Tooltip("Circle opening back up over the new scene.")]
    [SerializeField] private float _openDuration = 0.45f;

    private void Awake()
    {
        var button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(Load);
    }

    public void Load()
    {
        SceneTransition.LoadScene(_sceneName, _blackHold, _closeDuration, _openDuration);
    }
}
