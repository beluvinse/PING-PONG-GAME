using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Scene changes with a circular wipe: black closes in from the edges of the
/// screen, the next scene loads behind it, then it opens again.
/// Builds its own canvas at runtime and survives the load, so neither scene
/// needs any setup - just call <see cref="LoadScene"/>.
/// </summary>
public class SceneTransition : MonoBehaviour
{
    // Radius in screen halves: 0 covers everything, 1 reaches the corners, and
    // a little past that clears the soft edge.
    private const float OPEN = 1.2f;
    private const float CLOSED = 0f;

    [SerializeField] private float _closeDuration = 0.45f;
    [SerializeField] private float _openDuration = 0.45f;
    [SerializeField] private float _blackHold = 1f;
    [SerializeField] private Color _color = Color.black;

    private static SceneTransition _instance;

    private RawImage _overlay;
    private Material _material;
    private bool _busy;

    /// <summary>True while a wipe is running.</summary>
    public static bool IsBusy => _instance != null && _instance._busy;

    /// <summary>Any negative duration keeps this component's own default.</summary>
    public static void LoadScene(string sceneName, float blackHold = -1f, float closeDuration = -1f, float openDuration = -1f)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("SceneTransition: no scene name given.");
            return;
        }

        Bootstrap();
        if (_instance == null || _instance._busy) return;   // also swallows double clicks

        _instance.StartCoroutine(_instance.Run(
            sceneName,
            blackHold < 0f ? _instance._blackHold : blackHold,
            closeDuration < 0f ? _instance._closeDuration : closeDuration,
            openDuration < 0f ? _instance._openDuration : openDuration));
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("SceneTransition");
        DontDestroyOnLoad(go);
        go.AddComponent<SceneTransition>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        Build();
    }

    private void Build()
    {
        var shader = Shader.Find("Custom/IrisWipe");
        if (shader == null)
        {
            Debug.LogWarning("SceneTransition: shader 'Custom/IrisWipe' not found - scenes will load without the wipe.");
            return;
        }

        var canvasGo = new GameObject("TransitionCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;      // on top of every other canvas

        var overlayGo = new GameObject("Iris");
        overlayGo.transform.SetParent(canvasGo.transform, false);
        _overlay = overlayGo.AddComponent<RawImage>();
        _overlay.raycastTarget = false;            // only blocks clicks mid-wipe

        var rect = _overlay.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        _material = new Material(shader);
        _material.SetColor("_Color", _color);
        _overlay.material = _material;
        SetRadius(OPEN);
    }

    private IEnumerator Run(string sceneName, float blackHold, float closeDuration, float openDuration)
    {
        _busy = true;
        if (_overlay != null) _overlay.raycastTarget = true;

        yield return Animate(OPEN, CLOSED, closeDuration);

        var load = SceneManager.LoadSceneAsync(sceneName);
        if (load == null)
        {
            // Almost always means the scene is missing from Build Settings.
            Debug.LogError($"SceneTransition: could not load '{sceneName}'. Is it in File > Build Settings?");
            yield return Animate(CLOSED, OPEN, openDuration);
            if (_overlay != null) _overlay.raycastTarget = false;
            _busy = false;
            yield break;
        }

        // Load while the screen is black, but hold the new scene back until the
        // beat is over, so the wait reads as loading and never flashes early.
        load.allowSceneActivation = false;
        var held = 0f;
        while (held < blackHold || load.progress < 0.9f)
        {
            held += Time.unscaledDeltaTime;
            yield return null;
        }

        load.allowSceneActivation = true;
        while (!load.isDone) yield return null;
        yield return null;                          // let the new scene draw one frame

        yield return Animate(CLOSED, OPEN, openDuration);

        if (_overlay != null) _overlay.raycastTarget = false;
        _busy = false;
    }

    private IEnumerator Animate(float from, float to, float duration)
    {
        if (_material == null || duration <= 0f)
        {
            SetRadius(to);
            yield break;
        }

        var elapsed = 0f;
        while (elapsed < duration)
        {
            // Unscaled: the game may be paused (timeScale 0) when leaving a match.
            elapsed += Time.unscaledDeltaTime;
            SetRadius(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / duration)));
            yield return null;
        }

        SetRadius(to);
    }

    private void SetRadius(float radius)
    {
        if (_material == null) return;
        _material.SetFloat("_Radius", radius);
        _material.SetFloat("_Aspect", Screen.width / (float)Mathf.Max(1, Screen.height));
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }
}
