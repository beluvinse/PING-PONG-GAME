using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("References")] 
    [SerializeField] private MatchController _matchController;

    [SerializeField] private GameObject _pausePanel;
    [SerializeField] private Image _scorerPanel;
    [SerializeField] private Color _redColor;
    [SerializeField] private Color _blueColor;
    [SerializeField] private UIPanelAnimations _panelAnimations;

    [Header("Score UI")]
    [SerializeField] private TextMeshProUGUI[] _playerScoreText;
    [SerializeField] private TextMeshProUGUI[] _aiScoreText;
    [SerializeField] private TextMeshProUGUI _scorerText;
    [SerializeField] private TextMeshProUGUI _serverText;
    [SerializeField] private TextMeshProUGUI _firstTo5Text;

    [Header("Buttons")] 
    [SerializeField] private Button _rematchButton;
    [SerializeField] private Button _quitButton;
    [SerializeField] private Button _quitButtonPause;

    [SerializeField] private float _messagesDuration = 1.5f;

    [Header("Rule Text Drop")]
    [Tooltip("How far above its place each letter of FIRST TO 5 / DEUCE / MATCH POINT starts.")]
    [SerializeField] private float _ruleDropHeight = 45f;
    [Tooltip("Seconds between one letter starting to fall and the next.")]
    [SerializeField] private float _ruleLetterDelay = 0.05f;
    [Tooltip("Seconds each letter takes to land.")]
    [SerializeField] private float _ruleLetterDuration = 0.35f;
    [Tooltip("Overshoot when a letter lands. 0 = no bounce.")]
    [SerializeField] private float _ruleBounce = 1.7f;

    [Header("Match End Screen")]
    [Tooltip("Full-screen modal shown when a match ends. Built by Tools > Ping Pong > Build Match End Screen.")]
    [SerializeField] private GameObject _matchEndScreen;
    [Tooltip("The card inside the screen; it pops in and out.")]
    [SerializeField] private RectTransform _matchEndCard;
    [Tooltip("Image that shows the result title.")]
    [SerializeField] private Image _matchEndTitle;
    [SerializeField] private Sprite _winTitleSprite;
    [SerializeField] private Sprite _loseTitleSprite;
    [SerializeField] private TextMeshProUGUI _matchEndPlayerScore;
    [SerializeField] private TextMeshProUGUI _matchEndOpponentScore;
    [SerializeField] private Button _matchEndRematchButton;
    [SerializeField] private Button _matchEndMenuButton;
    [Tooltip("Scene the Menu button goes to. Must be in Build Settings.")]
    [SerializeField] private string _menuSceneName = "Menu";
    [Tooltip("Seconds between the winning point and the screen, so the final score is seen landing.")]
    [SerializeField] private float _matchEndDelay = 1f;
    [Tooltip("Full-screen image behind the card (bg2). Empty = the Image on MatchEndScreen itself.")]
    [SerializeField] private Image _matchEndBackground;
    [Tooltip("Seconds the background takes to fade in. The card pops in partway through.")]
    [SerializeField] private float _matchEndBackgroundFade = 0.6f;

    [Header("Win Stars")]
    [Tooltip("Sprite for each star. Empty = no stars.")]
    [SerializeField] private Sprite _starSprite;
    [Tooltip("Where the stars burst from. Empty = the middle of the screen.")]
    [SerializeField] private RectTransform _starOrigin;
    [SerializeField] private bool _starsInFrontOfCard = true;
    [SerializeField] private int _starCount = 28;
    [Tooltip("Launch speed range, in canvas units per second.")]
    [SerializeField] private Vector2 _starSpeed = new Vector2(600f, 1300f);
    [Tooltip("0 = thrown evenly in every direction, higher = more of them go up first.")]
    [SerializeField] private float _starUpwardBias = 0.6f;
    [Tooltip("Pull downwards, in canvas units per second squared.")]
    [SerializeField] private float _starGravity = 1800f;
    [Tooltip("Seconds each star lives, picked in this range.")]
    [SerializeField] private Vector2 _starLifetime = new Vector2(1.1f, 1.7f);
    [Tooltip("Size a star reaches at its biggest, picked in this range.")]
    [SerializeField] private Vector2 _starSize = new Vector2(40f, 100f);
    [Tooltip("Stars leave over this many seconds instead of all in the same frame.")]
    [SerializeField] private float _starStagger = 0.15f;
    [SerializeField] private Color _starColor = Color.white;

    [Header("Lose Particles")]
    [Tooltip("Camera + ParticleSystem rig for YOU LOSE. Built by Tools > Ping Pong > Build Lose Particles.")]
    [SerializeField] private GameObject _loseParticlesRig;
    [SerializeField] private ParticleSystem _loseParticles;
    [Tooltip("RawImage in the end screen that shows what the rig's camera renders.")]
    [SerializeField] private RawImage _loseParticlesView;

    private const string PLAYER_SCORED_TEXT = "You scored!";
    private const string AI_SCORED_TEXT = "Opponent scored!";
    private const string VICTORY_TEXT = "VICTORY";
    private const string DEFEAT_TEXT = "DEFEAT";
    private const string PLAYER_SERVES_TEXT = "Your serve";
    private const string OPPONENT_SERVES_TEXT = "Opponent to serve";
    private const string RESUME_LABEL = "Resume";
    private const string MAIN_MENU_LABEL = "Main menu";

    private const string GAME_FIRSTTO5 = "FIRST TO 5";
    private const string GAME_MATCHPOINT = "MATCH POINT";
    private const string GAME_DEUCE = "DEUCE";

    // Once both players reach this, winning needs a two-point lead.
    private const int DEUCE_SCORE = 4;

    private Button _resumeButton;
    private Coroutine _scorerTypeRoutine;
    private Sequence _ruleDrop;
    private float[] _ruleLetterProgress = new float[0];
    private TMP_MeshInfo[] _ruleRestingMesh;
    private bool _ruleDropWaiting;
    private Sequence _matchEndTween;
    private float _matchEndBackgroundAlpha = -1f;
    private Sequence _starBurst;
    private RectTransform _starContainer;
    private readonly List<Image> _stars = new List<Image>();

    private void Awake()
    {
        SetUpListeners();
        BuildPauseMenu();

        // Auto-start serves on its own, so the Play/Quit panel would only be in
        // the way; without it, that panel is the only way into a match.
        if (_matchEndScreen != null) _matchEndScreen.SetActive(false);
        StopLoseParticles();
        _pausePanel.SetActive(false);
        _scorerText.text = "";
        // Always on screen; every new wording drops in letter by letter.
        _firstTo5Text.gameObject.SetActive(true);
        _firstTo5Text.maxVisibleCharacters = int.MaxValue;
        SetRuleText();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            TogglePause();

        // The first drop waits for the scene wipe to open, or it would play in the dark.
        if (_ruleDropWaiting)
        {
            if (SceneTransition.IsBusy)
            {
                ApplyRuleLetters();
            }
            else
            {
                _ruleDropWaiting = false;
                _ruleDrop.Play();
            }
        }
    }

    private void SetUpListeners()
    {
        _matchController.OnBallServed += OnBallServed;
        _matchController.OnServerAnnounced += OnServerAnnounced;
        _matchController.OnPointWon += OnPointWon;
        _matchController.OnRallyStarted += OnRallyStarted;
        _matchController.OnMatchOver += OnMatchOver;

        _rematchButton.onClick.AddListener(OnRematchClicked);
        _quitButton.onClick.AddListener(OnQuitClicked);
        _quitButtonPause.onClick.AddListener(OnMainMenuClicked);

        if (_matchEndRematchButton != null) _matchEndRematchButton.onClick.AddListener(OnMatchEndRematchClicked);
        if (_matchEndMenuButton != null) _matchEndMenuButton.onClick.AddListener(OnMatchEndMenuClicked);
    }

    private void BuildPauseMenu()
    {
        // The pause panel originally only had a quit-to-desktop button.
        // Repurpose it as "Main menu" and clone it to add a "Resume" button above.
        SetButtonLabel(_quitButtonPause, MAIN_MENU_LABEL);

        _resumeButton = Instantiate(_quitButtonPause, _quitButtonPause.transform.parent);
        _resumeButton.name = "ResumeButton";
        SetButtonLabel(_resumeButton, RESUME_LABEL);
        _resumeButton.onClick.AddListener(TogglePause);

        var quitRect = _quitButtonPause.GetComponent<RectTransform>();
        var resumeRect = _resumeButton.GetComponent<RectTransform>();
        resumeRect.anchoredPosition = quitRect.anchoredPosition + Vector2.up * (quitRect.rect.height + 14f);
    }

    private static void SetButtonLabel(Button button, string label)
    {
        var tmpLabel = button.GetComponentInChildren<TMP_Text>(true);
        if (tmpLabel != null)
        {
            tmpLabel.text = label;
            return;
        }

        var legacyLabel = button.GetComponentInChildren<Text>(true);
        if (legacyLabel != null)
            legacyLabel.text = label;
    }

    private void TogglePause()
    {
        if (_matchEndScreen != null && _matchEndScreen.activeSelf) return;

        _pausePanel.SetActive(!_pausePanel.activeSelf);
        Time.timeScale = _pausePanel.activeSelf ? 0 : 1;
    }

    private void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
    }

    private void OnMainMenuClicked()
    {
        Time.timeScale = 1f;
        _pausePanel.SetActive(false);

        StopAllCoroutines();
        _scorerTypeRoutine = null;
        _scorerText.text = "";
        _scorerText.maxVisibleCharacters = int.MaxValue;
        ResetScores();

        _matchController.StopMatch();
        SetRuleText();

        // StartRally parked the end screen off-screen, so it has to be put back
        // in place rather than just switched on.
        _panelAnimations.ShowGameEndedImmediate();
    }

    private void OnRematchClicked()
    {
        ResetScores();
        // RestartGame reaches OnRallyStarted, and StartRally slides the end
        // screen away and switches it off once it is gone.
        _matchController.RestartGame();
        SetRuleText();
    }
    
    private void OnMatchOver(MatchController.Side winner)
    {
        if (_matchEndScreen != null)
        {
            ShowMatchEnd(winner);
            return;
        }

        // Until the end screen is built, the old VICTORY / DEFEAT flow still runs.
        _panelAnimations.MatchOver();
    }

    private void ShowMatchEnd(MatchController.Side winner)
    {
        if (_matchEndTitle != null)
            _matchEndTitle.sprite = winner == MatchController.Side.Player ? _winTitleSprite : _loseTitleSprite;
        if (_matchEndPlayerScore != null) _matchEndPlayerScore.text = _matchController.playerScore.ToString();
        if (_matchEndOpponentScore != null) _matchEndOpponentScore.text = _matchController.aiScore.ToString();

        var screen = MatchEndGroup();
        var background = MatchEndBackground();
        var card = CardGroup();
        _matchEndTween?.Kill();
        _matchEndScreen.SetActive(true);

        // The screen itself is fully on; the background and the card fade separately.
        screen.alpha = 1f;
        screen.interactable = false;         // no clicks until it has landed
        screen.blocksRaycasts = true;

        if (background != null)
        {
            // Fade up to whatever alpha the image was given in the scene.
            if (_matchEndBackgroundAlpha < 0f) _matchEndBackgroundAlpha = background.color.a;
            SetAlpha(background, 0f);
        }
        if (card != null) card.alpha = 0f;
        if (_matchEndCard != null) _matchEndCard.localScale = Vector3.one * 0.6f;

        // Background eases in first; the card pops while it is still settling.
        var backgroundFade = background != null ? _matchEndBackgroundFade : 0f;
        var cardStart = _matchEndDelay + backgroundFade * 0.6f;

        _matchEndTween = DOTween.Sequence().SetUpdate(true);
        if (background != null)
            _matchEndTween.Insert(_matchEndDelay, background.DOFade(_matchEndBackgroundAlpha, backgroundFade).SetEase(Ease.OutQuad));
        if (card != null)
            _matchEndTween.Insert(cardStart, card.DOFade(1f, 0.25f));
        if (_matchEndCard != null)
            _matchEndTween.Insert(cardStart, _matchEndCard.DOScale(1f, 0.45f).SetEase(Ease.OutBack));
        if (winner == MatchController.Side.Player)
            _matchEndTween.InsertCallback(cardStart, BurstStars);
        else
            _matchEndTween.InsertCallback(cardStart, PlayLoseParticles);
        _matchEndTween.OnComplete(() => screen.interactable = true);
    }

    /// <summary>
    /// Throws _starCount stars out from _starOrigin. Each one is a pooled Image
    /// driven by its own tween: it flies out, falls under gravity and spins like
    /// confetti, grows then shrinks, and fades out over the second half of its life.
    /// </summary>
    private void BurstStars()
    {
        if (_starSprite == null) return;

        var container = StarContainer();
        // Last first, so the card's index is not shifted when moving the stars just before it.
        container.SetAsLastSibling();
        if (!_starsInFrontOfCard && _matchEndCard != null)
            container.SetSiblingIndex(_matchEndCard.GetSiblingIndex());

        var origin = _starOrigin != null ? (Vector2)container.InverseTransformPoint(_starOrigin.position) : Vector2.zero;

        StopStars();
        _starBurst = DOTween.Sequence().SetUpdate(true);
        for (var i = 0; i < _starCount; i++)
            _starBurst.Insert(Random.Range(0f, _starStagger), StarTween(StarAt(container, i), origin));
    }

    private Tween StarTween(Image star, Vector2 origin)
    {
        var rect = star.rectTransform;

        var direction = Random.insideUnitCircle.normalized;
        direction.y += _starUpwardBias;
        var velocity = direction.normalized * Random.Range(_starSpeed.x, _starSpeed.y);

        var life = Random.Range(_starLifetime.x, _starLifetime.y);
        var startAngle = Random.Range(0f, 360f);
        var spin = Random.Range(-360f, 360f);
        rect.sizeDelta = Vector2.one * Random.Range(_starSize.x, _starSize.y);

        var progress = 0f;
        return DOTween.To(() => progress, p => progress = p, 1f, life)
            .SetEase(Ease.Linear)
            .OnStart(() => star.gameObject.SetActive(true))
            .OnUpdate(() =>
            {
                var time = progress * life;
                rect.anchoredPosition = origin + velocity * time + Vector2.down * (0.5f * _starGravity * time * time);
                rect.localRotation = Quaternion.Euler(0f, 0f, startAngle + spin * time);

                // Small -> big -> small, and fully gone by the end.
                rect.localScale = Vector3.one * Mathf.Sin(Mathf.PI * progress);
                SetAlpha(star, _starColor.a * (1f - Mathf.SmoothStep(0f, 1f, (progress - 0.35f) / 0.65f)));
            })
            .OnComplete(() => star.gameObject.SetActive(false));
    }

    private RectTransform StarContainer()
    {
        if (_starContainer != null) return _starContainer;

        var go = new GameObject("WinStars", typeof(RectTransform));
        go.transform.SetParent(_matchEndScreen.transform, false);

        _starContainer = (RectTransform)go.transform;
        _starContainer.anchorMin = Vector2.zero;
        _starContainer.anchorMax = Vector2.one;
        _starContainer.offsetMin = Vector2.zero;
        _starContainer.offsetMax = Vector2.zero;
        return _starContainer;
    }

    private Image StarAt(RectTransform container, int index)
    {
        while (_stars.Count <= index)
        {
            var go = new GameObject("Star", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(container, false);
            go.SetActive(false);

            var image = go.GetComponent<Image>();
            image.raycastTarget = false;        // never in the way of the buttons
            image.preserveAspect = true;
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _stars.Add(image);
        }

        var star = _stars[index];
        star.sprite = _starSprite;
        star.color = _starColor;
        star.rectTransform.localScale = Vector3.zero;
        return star;
    }

    /// <summary>
    /// The YOU LOSE particles are a regular ParticleSystem designed in the Inspector.
    /// The overlay canvas would draw over them, so the rig's own camera renders
    /// them into a RenderTexture that _loseParticlesView shows. The rig, camera
    /// included, only runs while the screen is up.
    /// </summary>
    private void PlayLoseParticles()
    {
        if (_loseParticles == null) return;

        if (_loseParticlesRig != null) _loseParticlesRig.SetActive(true);
        if (_loseParticlesView != null)
        {
            _loseParticlesView.enabled = true;
            SetAlpha(_loseParticlesView, 1f);
        }

        _loseParticles.Clear(true);
        _loseParticles.Play(true);
    }

    private void StopLoseParticles()
    {
        if (_loseParticles != null) _loseParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (_loseParticlesView != null) _loseParticlesView.enabled = false;
        if (_loseParticlesRig != null) _loseParticlesRig.SetActive(false);
    }

    private void StopStars()
    {
        _starBurst?.Kill();
        foreach (var star in _stars)
            if (star != null) star.gameObject.SetActive(false);
    }

    private void HideMatchEnd(TweenCallback onHidden)
    {
        var screen = MatchEndGroup();
        var background = MatchEndBackground();
        var card = CardGroup();
        _matchEndTween?.Kill();
        StopStars();
        screen.interactable = false;

        // The reverse: the card leaves first, then the background fades out.
        _matchEndTween = DOTween.Sequence().SetUpdate(true);
        if (card != null)
            _matchEndTween.Insert(0f, card.DOFade(0f, 0.2f));
        if (_matchEndCard != null)
            _matchEndTween.Insert(0f, _matchEndCard.DOScale(0.85f, 0.2f).SetEase(Ease.InBack));
        if (background != null)
            _matchEndTween.Insert(0.1f, background.DOFade(0f, _matchEndBackgroundFade * 0.5f));
        if (_loseParticlesView != null && _loseParticlesView.enabled)
            _matchEndTween.Insert(0f, _loseParticlesView.DOFade(0f, 0.25f));
        _matchEndTween.OnComplete(() =>
        {
            _matchEndScreen.SetActive(false);
            StopLoseParticles();
            onHidden?.Invoke();
        });
    }

    private Image MatchEndBackground() =>
        _matchEndBackground != null ? _matchEndBackground : _matchEndScreen.GetComponent<Image>();

    // The card fades on its own group so the background can take longer than it.
    private CanvasGroup CardGroup()
    {
        if (_matchEndCard == null) return null;

        var group = _matchEndCard.GetComponent<CanvasGroup>();
        return group != null ? group : _matchEndCard.gameObject.AddComponent<CanvasGroup>();
    }

    private static void SetAlpha(Graphic graphic, float alpha)
    {
        var color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }

    private CanvasGroup MatchEndGroup()
    {
        var group = _matchEndScreen.GetComponent<CanvasGroup>();
        return group != null ? group : _matchEndScreen.AddComponent<CanvasGroup>();
    }

    private void OnMatchEndRematchClicked() => HideMatchEnd(OnRematchClicked);

    private void OnMatchEndMenuClicked()
    {
        MatchEndGroup().interactable = false;    // the load takes a moment; no double clicks
        Time.timeScale = 1f;
        SceneTransition.LoadScene(_menuSceneName);
    }

    private void OnRallyStarted()
    {
        // StartRally is what clears the end screen, so it only runs when that
        // screen is actually up.
            _panelAnimations.StartRally();

        _panelAnimations.ShowServerInfo();
    }

    private void OnPointWon(MatchController.Side scorerSide)
    {
        var newText = scorerSide.Equals(MatchController.Side.Player) ? PLAYER_SCORED_TEXT : AI_SCORED_TEXT;
        _scorerText.text = newText;

        if (_scorerTypeRoutine != null)
            StopCoroutine(_scorerTypeRoutine);
        _scorerTypeRoutine = StartCoroutine(TypeText(_scorerText, newText));
        StartCoroutine(UpdateScore(scorerSide, .5f));

        ImpactEffects.Instance.Shake(0.03f, 0.45f);
    }

    private void OnServerAnnounced(MatchController.Side serverSide)
    {
        _serverText.text = serverSide.Equals(MatchController.Side.Player) ? PLAYER_SERVES_TEXT : OPPONENT_SERVES_TEXT;
        _scorerPanel.color = serverSide.Equals(MatchController.Side.Player) ? _redColor : _blueColor;
    }

    private void OnBallServed(bool ballServed)
    {
        if (!ballServed) return;
        _scorerText.text = "";
        _scorerText.maxVisibleCharacters = int.MaxValue;
    }

    private IEnumerator TextPop(TextMeshProUGUI text)
    {
        text.gameObject.SetActive(true);
        var color = text.color;
        color.a = 1f;
        text.color = color;

        var rect = text.rectTransform;
        rect.localScale = Vector3.zero;

        yield return ScaleTo(rect, 1.3f, 0.15f);
        yield return ScaleTo(rect, 0.9f, 0.1f);
        yield return ScaleTo(rect, 1f, 0.08f);

        yield return new WaitForSeconds(_messagesDuration);

        var fadeDuration = 0.3f;
        var elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            color.a = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            text.color = color;
            yield return null;
        }

        color.a = 0f;
        text.color = color;

        text.gameObject.SetActive(false);
    }

    private IEnumerator ScaleTo(RectTransform rect, float targetScale, float duration)
    {
        var start = rect.localScale;
        var target = Vector3.one * targetScale;
        var elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            rect.localScale = Vector3.Lerp(
                start,
                target,
                elapsed / duration
            );
            yield return null;
        }

        rect.localScale = target;
    }

    private IEnumerator UpdateScore(MatchController.Side scoreSide, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        var texts = scoreSide == MatchController.Side.Player ? _playerScoreText : _aiScoreText;
        var score = scoreSide == MatchController.Side.Player
            ? _matchController.playerScore.ToString()
            : _matchController.aiScore.ToString();

        // The first text gets the bump animation, any others are just set.
        // Empty slots are skipped: the side panel's copy of the score is gone.
        for (var i = 0; i < texts.Length; i++)
        {
            if (texts[i] == null) continue;

            if (i == 0) StartCoroutine(AnimateScore(texts[i], score));
            else texts[i].text = score;
        }

        // Follows the score that is on screen, not the one about to be.
        SetRuleText();
    }

    /// <summary>
    /// FIRST TO 5 normally. From 4-4 on: DEUCE while level, MATCH POINT while one
    /// player is a point ahead. Once the match is decided the last wording stays.
    /// </summary>
    private void SetRuleText()
    {
        if (_matchController.IsMatchOver()) return;

        var player = _matchController.playerScore;
        var ai = _matchController.aiScore;
        var pastDeuce = player >= DEUCE_SCORE && ai >= DEUCE_SCORE;

        var wording = !pastDeuce ? GAME_FIRSTTO5
                    : player == ai ? GAME_DEUCE
                    : GAME_MATCHPOINT;

        ShowRuleText(wording);
    }

    /// <summary>
    /// Drops a new wording in letter by letter. A DOTween sequence tweens each
    /// letter's progress from 0 to 1, staggered by _ruleLetterDelay, and every
    /// update moves that letter's vertices down into place and fades it in.
    /// The same wording again does nothing, so the text stays still between points.
    /// </summary>
    private void ShowRuleText(string wording)
    {
        if (_ruleDrop != null && _firstTo5Text.text == wording) return;

        _ruleDrop?.Kill();
        _firstTo5Text.text = wording;
        _firstTo5Text.ForceMeshUpdate();
        _ruleRestingMesh = _firstTo5Text.textInfo.CopyMeshInfoVertexData();

        var letters = 0;
        var info = _firstTo5Text.textInfo;
        for (var i = 0; i < info.characterCount; i++)
            if (info.characterInfo[i].isVisible) letters++;     // spaces don't take a turn

        _ruleLetterProgress = new float[letters];
        _ruleDrop = DOTween.Sequence().SetUpdate(true);         // unscaled: finishes even while paused

        for (var i = 0; i < letters; i++)
        {
            var letter = i;
            _ruleDrop.Insert(letter * _ruleLetterDelay,
                DOTween.To(() => _ruleLetterProgress[letter], x => _ruleLetterProgress[letter] = x, 1f, _ruleLetterDuration)
                       .SetEase(Ease.Linear));
        }

        _ruleDrop.OnUpdate(ApplyRuleLetters)
                 .OnComplete(() => _firstTo5Text.ForceMeshUpdate());   // back to TMP's own, untouched mesh

        // Hide the letters in the same frame the text changed, before the canvas draws it whole.
        ApplyRuleLetters();

        _ruleDropWaiting = SceneTransition.IsBusy;
        if (_ruleDropWaiting) _ruleDrop.Pause();
    }

    private void ApplyRuleLetters()
    {
        var info = _firstTo5Text.textInfo;
        var letter = 0;

        for (var i = 0; i < info.characterCount && letter < _ruleLetterProgress.Length; i++)
        {
            var character = info.characterInfo[i];
            if (!character.isVisible) continue;

            var progress = _ruleLetterProgress[letter++];
            var fall = DOVirtual.EasedValue(0f, 1f, progress, Ease.OutBack, _ruleBounce);
            var offset = new Vector3(0f, (1f - fall) * _ruleDropHeight, 0f);
            var alpha = Mathf.SmoothStep(0f, 1f, progress * 2f);   // fully visible by halfway down

            if (character.materialReferenceIndex >= _ruleRestingMesh.Length) continue;
            var current = info.meshInfo[character.materialReferenceIndex];
            var resting = _ruleRestingMesh[character.materialReferenceIndex];
            var last = character.vertexIndex + 3;
            if (last >= current.vertices.Length || last >= resting.vertices.Length) continue;

            for (var v = character.vertexIndex; v <= last; v++)
            {
                current.vertices[v] = resting.vertices[v] + offset;

                var color = resting.colors32[v];
                color.a = (byte)(color.a * alpha);
                current.colors32[v] = color;
            }
        }

        _firstTo5Text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
    }

    private void ResetScores()
    {
        foreach (var t in _playerScoreText)
            if (t != null) t.text = "0";
        foreach (var t in _aiScoreText)
            if (t != null) t.text = "0";
    }

    private IEnumerator AnimateScore(TextMeshProUGUI text, string newValue)
    {
        var rect = text.rectTransform;
        var originalPos = rect.anchoredPosition;
        var topPos = originalPos + Vector2.up * 40f;
        var duration = 0.15f;
        var t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            rect.anchoredPosition = Vector2.Lerp(originalPos, topPos, t / duration);
            yield return null;
        }

        text.text = newValue;
        rect.localScale = Vector3.one * 1.35f;
        t = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            var progress = t / duration;
            rect.anchoredPosition = Vector2.Lerp(topPos, originalPos, progress);
            rect.localScale = Vector3.Lerp(Vector3.one * 1.35f, Vector3.one, progress);
            yield return null;
        }

        rect.anchoredPosition = originalPos;
        rect.localScale = Vector3.one;
    }

    private IEnumerator TypeText(TextMeshProUGUI text, string message)
    {
        text.text = message;

        text.maxVisibleCharacters = 0;

        while (text.maxVisibleCharacters < message.Length)
        {
            text.maxVisibleCharacters++;
            yield return new WaitForSeconds(0.05f);
        }

        yield return new WaitForSeconds(_messagesDuration);

        while (text.maxVisibleCharacters > 0)
        {
            text.maxVisibleCharacters--;
            yield return new WaitForSeconds(0.03f);
        }
    }

    private void OnDestroy()
    {
        _ruleDrop?.Kill();
        _matchEndTween?.Kill();
        _starBurst?.Kill();
        if (_matchEndRematchButton != null) _matchEndRematchButton.onClick.RemoveAllListeners();
        if (_matchEndMenuButton != null) _matchEndMenuButton.onClick.RemoveAllListeners();

        _matchController.OnBallServed -= OnBallServed;
        _matchController.OnServerAnnounced -= OnServerAnnounced;
        _matchController.OnPointWon -= OnPointWon;
        _matchController.OnRallyStarted -= OnRallyStarted;
        _matchController.OnMatchOver -= OnMatchOver;

        _rematchButton.onClick.RemoveAllListeners();
        _quitButton.onClick.RemoveAllListeners();
        _quitButtonPause.onClick.RemoveAllListeners();
        if (_resumeButton != null)
            _resumeButton.onClick.RemoveAllListeners();
    }
}