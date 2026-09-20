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
    [SerializeField] private TextMeshProUGUI _serverText;
    [SerializeField] private TextMeshProUGUI _firstTo5Text;

    [Header("Pause Menu")] 
    [SerializeField] private Button _resumeButton;
    [SerializeField] private Button _restartButton;
    [SerializeField] private Button _settingsButton;
    [SerializeField] private Button _pauseMenuButton;
    [Tooltip("Options screen inside this scene. Empty = the Settings button does nothing.")]
    [SerializeField] private GameObject _settingsScreen;

    [Header("Point Feedback")]
    [Tooltip("Seconds the old number stays on screen before it leaves.")]
    [SerializeField] private float _scoreHold = 0.15f;
    [Tooltip("How far the old number rises as it goes, and how far below the new one starts.")]
    [SerializeField] private float _scoreTravel = 45f;
    [Tooltip("Seconds the old number takes to leave, and the new one to arrive.")]
    [SerializeField] private float _scoreSwap = 0.16f;
    [Tooltip("How hard the card of whoever scored bounces. 0 = no bounce.")]
    [SerializeField] private float _scoreCardPunch = 0.07f;
    [Tooltip("Star burst from behind the number. Built by Tools > Ping Pong > Add Point Sparkles.")]
    [SerializeField] private GameObject _pointSparklesRig;
    [SerializeField] private ParticleSystem _pointSparkles;
    [Tooltip("Full-screen RawImage showing what the sparkle rig's camera renders.")]
    [SerializeField] private RawImage _pointSparklesView;

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

    [Header("Match End Winner")]
    [Tooltip("The paddles in the end screen. The winner's one grows and gets the crown.")]
    [SerializeField] private RectTransform _matchEndPlayerPaddle;
    [SerializeField] private RectTransform _matchEndOpponentPaddle;
    [Tooltip("Crown over each paddle, hidden until that side wins. Added by Tools > Ping Pong > Add Winner Crowns.")]
    [SerializeField] private RectTransform _matchEndPlayerCrown;
    [SerializeField] private RectTransform _matchEndOpponentCrown;
    [Tooltip("How much wider and taller the winner's paddle gets. 1 = unchanged.")]
    [SerializeField] private float _matchEndWinnerScale = 1.25f;
    [Tooltip("Width and height of the loser's paddle, for contrast. 1 = unchanged.")]
    [SerializeField] private float _matchEndLoserScale = 1f;

    [Header("Win Particles")]
    [Tooltip("Camera + ParticleSystem rig for YOU WIN. Built by Tools > Ping Pong > Build Win Particles.")]
    [SerializeField] private GameObject _winParticlesRig;
    [SerializeField] private ParticleSystem _winParticles;
    [Tooltip("RawImage in the end screen that shows what the rig's camera renders.")]
    [SerializeField] private RawImage _winParticlesView;

    [Header("Lose Particles")]
    [Tooltip("Camera + ParticleSystem rig for YOU LOSE. Built by Tools > Ping Pong > Build Lose Particles.")]
    [SerializeField] private GameObject _loseParticlesRig;
    [SerializeField] private ParticleSystem _loseParticles;
    [Tooltip("RawImage in the end screen that shows what the rig's camera renders.")]
    [SerializeField] private RawImage _loseParticlesView;

    private const string PLAYER_SERVES_TEXT = "Your serve";
    private const string OPPONENT_SERVES_TEXT = "Opponent to serve";
    private const string GAME_FIRSTTO5 = "FIRST TO 5";
    private const string GAME_MATCHPOINT = "MATCH POINT";
    private const string GAME_DEUCE = "DEUCE";

    // Once both players reach this, winning needs a two-point lead.
    private const int DEUCE_SCORE = 4;

    private Camera _pointSparklesCamera;
    private Sequence _scoreTween;
    private Vector2 _playerScoreRest, _aiScoreRest;
    private RectTransform _playerScoreCard, _aiScoreCard;
    private Sequence _ruleDrop;
    private float[] _ruleLetterProgress = new float[0];
    private TMP_MeshInfo[] _ruleRestingMesh;
    private bool _ruleDropWaiting;
    private Sequence _matchEndTween;
    private float _matchEndBackgroundAlpha = -1f;
    private Vector2 _playerPaddleSize, _opponentPaddleSize, _playerCrownSize, _opponentCrownSize;

    private void Awake()
    {
        SetUpListeners();

        // Auto-start serves on its own, so the Play/Quit panel would only be in
        // the way; without it, that panel is the only way into a match.
        if (_matchEndScreen != null) _matchEndScreen.SetActive(false);
        StopMatchEndParticles();
        CacheWinnerSizes();
        _pausePanel.SetActive(false);
        CacheScoreAnimation();
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
        _matchController.OnMatchStarted += OnMatchStarted;
        if (_matchController.Countdown != null) _matchController.Countdown.Finished += OnCountdownFinished;

        if (_resumeButton != null) _resumeButton.onClick.AddListener(OnResumeClicked);
        if (_restartButton != null) _restartButton.onClick.AddListener(OnRestartClicked);
        if (_settingsButton != null) _settingsButton.onClick.AddListener(OnSettingsClicked);
        if (_pauseMenuButton != null) _pauseMenuButton.onClick.AddListener(LoadMenuScene);

        if (_matchEndRematchButton != null) _matchEndRematchButton.onClick.AddListener(OnMatchEndRematchClicked);
        if (_matchEndMenuButton != null) _matchEndMenuButton.onClick.AddListener(OnMatchEndMenuClicked);
    }

    private void TogglePause()
    {
        // Closing is always allowed; it is opening that has conditions.
        if (_pausePanel.activeSelf)
        {
            SetPaused(false);
            return;
        }

        if (CanPause) SetPaused(true);
    }

    /// <summary>
    /// Only while a match is actually being played. Pausing over the countdown or
    /// after the last point would freeze the game behind a screen whose Resume
    /// goes back to something that is no longer there.
    /// </summary>
    private bool CanPause
    {
        get
        {
            if (SceneTransition.IsBusy) return false;            // the wipe is still on screen
            if (_matchController.IsMatchOver()) return false;    // decided, even before the end screen is up
            if (_matchEndScreen != null && _matchEndScreen.activeSelf) return false;
            if (_settingsScreen != null && _settingsScreen.activeSelf) return false;   // Back is what closes it

            var countdown = _matchController.Countdown;
            return countdown == null || !countdown.IsRunning;
        }
    }

    /// <summary>
    /// Pausing freezes the match and sends the HUD off the top of the screen, so
    /// the menu is read against the court instead of over the scores.
    /// </summary>
    private void SetPaused(bool paused)
    {
        _pausePanel.SetActive(paused);
        Time.timeScale = paused ? 0f : 1f;
        _panelAnimations.ShowHud(!paused);
    }

    private void OnResumeClicked() => SetPaused(false);

    /// <summary>Restart: away with the menu, and the match begins again at 0-0.</summary>
    private void OnRestartClicked()
    {
        SetPaused(false);
        _matchController.RestartGame();      // OnMatchStarted clears the scores and parks the signs
    }

    /// <summary>
    /// Settings takes the pause menu's place rather than covering it, and time
    /// stays frozen underneath. Whatever closes the options screen calls
    /// <see cref="CloseSettings"/> to come back.
    /// </summary>
    private void OnSettingsClicked()
    {
        if (_settingsScreen == null) return;

        _pausePanel.SetActive(false);
        _settingsScreen.SetActive(true);
    }

    /// <summary>
    /// Public so the options screen's own Back and Apply buttons can be pointed at
    /// it in the Inspector.
    /// </summary>
    public void CloseSettings()
    {
        if (_settingsScreen != null) _settingsScreen.SetActive(false);
        _pausePanel.SetActive(true);
    }

    private void LoadMenuScene()
    {
        Time.timeScale = 1f;
        SceneTransition.LoadScene(_menuSceneName);
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

        // They leave on the same beat the end screen arrives, so the last point is read first.
        _panelAnimations.HideMatchSigns(_matchEndDelay);
    }

    private void ShowMatchEnd(MatchController.Side winner)
    {
        if (_matchEndTitle != null)
            _matchEndTitle.sprite = winner == MatchController.Side.Player ? _winTitleSprite : _loseTitleSprite;
        if (_matchEndPlayerScore != null) _matchEndPlayerScore.text = _matchController.playerScore.ToString();
        if (_matchEndOpponentScore != null) _matchEndOpponentScore.text = _matchController.aiScore.ToString();

        var playerWon = winner == MatchController.Side.Player;
        ResetWinnerMark(_matchEndPlayerPaddle, _playerPaddleSize, _matchEndPlayerCrown, _playerCrownSize, playerWon);
        ResetWinnerMark(_matchEndOpponentPaddle, _opponentPaddleSize, _matchEndOpponentCrown, _opponentCrownSize, !playerWon);

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
        if (playerWon)
            _matchEndTween.InsertCallback(cardStart, PlayWinParticles);
        else
            _matchEndTween.InsertCallback(cardStart, PlayLoseParticles);
        _matchEndTween.InsertCallback(cardStart, () => AudioManager.Play(playerWon ? SoundId.Victory : SoundId.Defeat));

        // Once the card has landed the winner's paddle swells, and the crown pops on after it.
        var winnerPaddle = playerWon ? _matchEndPlayerPaddle : _matchEndOpponentPaddle;
        var winnerCrown = playerWon ? _matchEndPlayerCrown : _matchEndOpponentCrown;
        var paddleSize = playerWon ? _playerPaddleSize : _opponentPaddleSize;
        var crownSize = playerWon ? _playerCrownSize : _opponentCrownSize;
        if (winnerPaddle != null)
            _matchEndTween.Insert(cardStart + 0.2f, winnerPaddle.DOSizeDelta(paddleSize * _matchEndWinnerScale, 0.5f).SetEase(Ease.OutBack));
        if (winnerCrown != null)
            _matchEndTween.Insert(cardStart + 0.35f, winnerCrown.DOSizeDelta(crownSize, 0.5f).SetEase(Ease.OutBack));

        _matchEndTween.OnComplete(() => screen.interactable = true);
    }

    /// <summary>
    /// Everything here grows by width and height, never by scale, so the sizes the
    /// scene rests at are read once - after the first match the paddles are carrying
    /// whatever size that winner left them at.
    /// </summary>
    private void CacheWinnerSizes()
    {
        if (_matchEndPlayerPaddle != null) _playerPaddleSize = _matchEndPlayerPaddle.sizeDelta;
        if (_matchEndOpponentPaddle != null) _opponentPaddleSize = _matchEndOpponentPaddle.sizeDelta;
        if (_matchEndPlayerCrown != null) _playerCrownSize = _matchEndPlayerCrown.sizeDelta;
        if (_matchEndOpponentCrown != null) _opponentCrownSize = _matchEndOpponentCrown.sizeDelta;
    }

    /// <summary>
    /// Says who won before the screen animates: the loser sits at its resting size
    /// with no crown, the winner starts at that same size with the crown still at no
    /// size at all, which is what the tweens in ShowMatchEnd grow from.
    /// </summary>
    private void ResetWinnerMark(RectTransform paddle, Vector2 paddleSize, RectTransform crown, Vector2 crownSize, bool won)
    {
        if (paddle != null) paddle.sizeDelta = paddleSize * (won ? 1f : _matchEndLoserScale);
        if (crown == null) return;

        crown.sizeDelta = won ? Vector2.zero : crownSize;
        crown.gameObject.SetActive(won);
    }

    /// <summary>
    /// Both end screens work the same way: a ParticleSystem designed in the Inspector
    /// with its own camera, because the overlay canvas draws over anything a camera
    /// renders. That camera renders into a RenderTexture shown by the screen's
    /// RawImage, and each rig only runs while its screen is up.
    /// </summary>
    private void PlayWinParticles() => PlayParticles(_winParticlesRig, _winParticles, _winParticlesView);

    private void PlayLoseParticles() => PlayParticles(_loseParticlesRig, _loseParticles, _loseParticlesView);

    private void StopMatchEndParticles()
    {
        StopParticles(_winParticlesRig, _winParticles, _winParticlesView);
        StopParticles(_loseParticlesRig, _loseParticles, _loseParticlesView);
    }

    private static void PlayParticles(GameObject rig, ParticleSystem particles, RawImage view)
    {
        if (particles == null) return;

        if (rig != null) rig.SetActive(true);
        if (view != null)
        {
            view.enabled = true;
            SetAlpha(view, 1f);
        }

        particles.Clear(true);
        particles.Play(true);
    }

    private static void StopParticles(GameObject rig, ParticleSystem particles, RawImage view)
    {
        if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (view != null) view.enabled = false;
        if (rig != null) rig.SetActive(false);
    }

    private void HideMatchEnd(TweenCallback onHidden)
    {
        var screen = MatchEndGroup();
        var background = MatchEndBackground();
        var card = CardGroup();
        _matchEndTween?.Kill();
        screen.interactable = false;

        // The reverse: the card leaves first, then the background fades out.
        _matchEndTween = DOTween.Sequence().SetUpdate(true);
        if (card != null)
            _matchEndTween.Insert(0f, card.DOFade(0f, 0.2f));
        if (_matchEndCard != null)
            _matchEndTween.Insert(0f, _matchEndCard.DOScale(0.85f, 0.2f).SetEase(Ease.InBack));
        if (background != null)
            _matchEndTween.Insert(0.1f, background.DOFade(0f, _matchEndBackgroundFade * 0.5f));
        foreach (var particleView in new[] { _winParticlesView, _loseParticlesView })
            if (particleView != null && particleView.enabled)
                _matchEndTween.Insert(0f, particleView.DOFade(0f, 0.25f));
        _matchEndTween.OnComplete(() =>
        {
            _matchEndScreen.SetActive(false);
            StopMatchEndParticles();
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
        LoadMenuScene();
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
        AudioManager.Play(scorerSide == MatchController.Side.Player ? SoundId.PointWon : SoundId.PointLost);
        ShowPointScored(scorerSide);

        ImpactEffects.Instance.Shake(0.03f, 0.45f);
    }

    private void OnServerAnnounced(MatchController.Side serverSide)
    {
        _serverText.text = serverSide.Equals(MatchController.Side.Player) ? PLAYER_SERVES_TEXT : OPPONENT_SERVES_TEXT;
        _scorerPanel.color = serverSide.Equals(MatchController.Side.Player) ? _redColor : _blueColor;
    }

    private void OnBallServed(bool ballServed)
    {
        // The ball is placed the moment the countdown ends, so this is also what
        // brings the signs in when no countdown runs, as in a rematch.
        _panelAnimations.ShowMatchSigns();
    }

    /// <summary>
    /// The numbers swap by moving, so where they rest has to be read before the
    /// first point moves them.
    /// </summary>
    private void CacheScoreAnimation()
    {
        if (MainScore(_playerScoreText) != null) _playerScoreRest = MainScore(_playerScoreText).rectTransform.anchoredPosition;
        if (MainScore(_aiScoreText) != null) _aiScoreRest = MainScore(_aiScoreText).rectTransform.anchoredPosition;
        _playerScoreCard = ScoreCard(_playerScoreText);
        _aiScoreCard = ScoreCard(_aiScoreText);

        // Asking the rig's camera beats hard-coding how much of the world it shows.
        if (_pointSparklesRig != null) _pointSparklesCamera = _pointSparklesRig.GetComponentInChildren<Camera>(true);
        StopParticles(_pointSparklesRig, _pointSparkles, _pointSparklesView);
    }

    /// <summary>
    /// The point lands on the number itself. The old score holds for a beat, lifts
    /// away and fades out; the new one rises into the empty place and punches
    /// 1 - 1.35 - 0.9 - 1. On that landing the panel bounces and a few big stars
    /// burst out from behind the number.
    /// </summary>
    private void ShowPointScored(MatchController.Side side)
    {
        var player = side == MatchController.Side.Player;
        var texts = player ? _playerScoreText : _aiScoreText;
        var score = (player ? _matchController.playerScore : _matchController.aiScore).ToString();
        var main = MainScore(texts);

        if (main == null)
        {
            SetScoreTexts(texts, score);
            SetRuleText();
            return;
        }

        _scoreTween?.Kill();
        ResetScoreVisuals();                                 // whatever a cut-short swap left behind

        var rect = main.rectTransform;
        var rest = player ? _playerScoreRest : _aiScoreRest;

        _scoreTween = DOTween.Sequence().SetUpdate(true);

        // The old number holds, then lifts away and is gone.
        _scoreTween.AppendInterval(_scoreHold);
        _scoreTween.Append(rect.DOAnchorPosY(rest.y + _scoreTravel, _scoreSwap).SetEase(Ease.InQuad));
        _scoreTween.Join(FadeText(main, 0f, _scoreSwap));

        // The new one is placed below the empty spot and rises into it.
        _scoreTween.AppendCallback(() =>
        {
            SetScoreTexts(texts, score);
            rect.anchoredPosition = new Vector2(rest.x, rest.y - _scoreTravel);
            SetRuleText();                                   // follows the number on screen
        });
        _scoreTween.Append(rect.DOAnchorPosY(rest.y, _scoreSwap).SetEase(Ease.OutCubic));
        _scoreTween.Join(FadeText(main, 1f, _scoreSwap * 0.7f));

        // It lands: stars, panel bounce and punch all go off together.
        _scoreTween.AppendCallback(() =>
        {
            PlayPointSparkles(rect);
            PunchCard(player ? _playerScoreCard : _aiScoreCard);
        });
        _scoreTween.Append(rect.DOScale(1.35f, 0.08f).SetEase(Ease.OutQuad));
        _scoreTween.Append(rect.DOScale(0.9f, 0.07f).SetEase(Ease.InOutQuad));
        _scoreTween.Append(rect.DOScale(1f, 0.06f).SetEase(Ease.OutQuad));
    }

    /// <summary>
    /// A short bounce of the card that just scored, and only that one: the panel
    /// holding both scores would take the other side along with it.
    /// </summary>
    private void PunchCard(RectTransform card)
    {
        if (card == null) return;

        card.DOKill();
        card.localScale = Vector3.one;
        card.DOPunchScale(Vector3.one * _scoreCardPunch, 0.35f, 6, 0.8f).SetUpdate(true);
    }

    /// <summary>The card a number sits on is its parent: Player / Score in the prefab.</summary>
    private static RectTransform ScoreCard(TextMeshProUGUI[] texts)
    {
        var main = MainScore(texts);
        return main != null ? main.rectTransform.parent as RectTransform : null;
    }

    /// <summary>The first filled slot is the score that is on screen and animated.</summary>
    private static TextMeshProUGUI MainScore(TextMeshProUGUI[] texts)
    {
        foreach (var text in texts)
            if (text != null) return text;

        return null;
    }

    /// <summary>Empty slots are skipped: the side panel's copy of the score is gone.</summary>
    private static void SetScoreTexts(TextMeshProUGUI[] texts, string score)
    {
        foreach (var text in texts)
            if (text != null) text.text = score;
    }

    /// <summary>DOTween's free build has no TMP module, so the alpha is tweened by hand.</summary>
    private static Tween FadeText(TextMeshProUGUI text, float alpha, float duration) =>
        DOTween.To(() => text.alpha, value => text.alpha = value, alpha, duration);

    /// <summary>Puts both numbers back where they belong, at full size and opacity.</summary>
    private void ResetScoreVisuals()
    {
        RestScore(_playerScoreText, _playerScoreRest);
        RestScore(_aiScoreText, _aiScoreRest);
    }

    private static void RestScore(TextMeshProUGUI[] texts, Vector2 rest)
    {
        var main = MainScore(texts);
        if (main == null) return;

        main.rectTransform.DOKill();
        main.rectTransform.anchoredPosition = rest;
        main.rectTransform.localScale = Vector3.one;
        main.alpha = 1f;

        var card = ScoreCard(texts);
        if (card == null) return;

        card.DOKill();
        card.localScale = Vector3.one;                       // a bounce cut short would have left it off
    }

    /// <summary>
    /// The stars live in a rig far from the table, like the end-screen ones, because
    /// an overlay canvas draws over anything a camera renders. That camera fills the
    /// screen, so where the number is on screen is where the burst goes in the rig.
    /// The rig switches itself off once the last star has died, camera included.
    /// </summary>
    private void PlayPointSparkles(RectTransform anchor)
    {
        if (_pointSparkles == null || _pointSparklesCamera == null) return;

        var onScreen = RectTransformUtility.WorldToScreenPoint(null, anchor.position);
        var height = _pointSparklesCamera.orthographicSize * 2f;
        _pointSparkles.transform.localPosition = new Vector3(
            (onScreen.x / Screen.width - 0.5f) * height * _pointSparklesCamera.aspect,
            (onScreen.y / Screen.height - 0.5f) * height,
            0f);

        PlayParticles(_pointSparklesRig, _pointSparkles, _pointSparklesView);

        // Everything leaves in the first burst, so the longest life is the whole show.
        DOVirtual.DelayedCall(_pointSparkles.main.startLifetime.constantMax + 0.1f,
            () => StopParticles(_pointSparklesRig, _pointSparkles, _pointSparklesView), true);
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

        // Here rather than in SetRuleText: this is where a wording is new, and the
        // stinger should not fire again on every point that keeps it up.
        if (wording == GAME_MATCHPOINT) AudioManager.Play(SoundId.MatchPoint);

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

    /// <summary>
    /// A new match is 0-0, whatever the scores said before. Awake runs before
    /// MatchController resets them, so it sees the scores saved in the scene; this
    /// puts the HUD and the rule text right once the match has really begun.
    /// </summary>
    private void OnMatchStarted()
    {
        ResetScores();
        SetRuleText();

        // Out of the way until the countdown lets them in. If there is no countdown
        // the first rally drops them, so they never stay stuck above the screen.
        _panelAnimations.ParkMatchSigns();
    }

    private void OnCountdownFinished() => _panelAnimations.ShowMatchSigns();

    private void ResetScores()
    {
        _scoreTween?.Kill();
        ResetScoreVisuals();

        foreach (var t in _playerScoreText)
            if (t != null) t.text = "0";
        foreach (var t in _aiScoreText)
            if (t != null) t.text = "0";
    }

    private void OnDestroy()
    {
        _ruleDrop?.Kill();
        _matchEndTween?.Kill();
        _scoreTween?.Kill();
        if (_matchEndRematchButton != null) _matchEndRematchButton.onClick.RemoveAllListeners();
        if (_matchEndMenuButton != null) _matchEndMenuButton.onClick.RemoveAllListeners();

        _matchController.OnBallServed -= OnBallServed;
        _matchController.OnServerAnnounced -= OnServerAnnounced;
        _matchController.OnPointWon -= OnPointWon;
        _matchController.OnRallyStarted -= OnRallyStarted;
        _matchController.OnMatchStarted -= OnMatchStarted;
        if (_matchController.Countdown != null) _matchController.Countdown.Finished -= OnCountdownFinished;
        _matchController.OnMatchOver -= OnMatchOver;

        foreach (var button in new[] { _resumeButton, _restartButton, _settingsButton, _pauseMenuButton })
            if (button != null) button.onClick.RemoveAllListeners();
    }
}