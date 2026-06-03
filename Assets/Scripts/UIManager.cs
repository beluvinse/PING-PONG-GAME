using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("References")] [SerializeField]
    private MatchController _matchController;

    [SerializeField] private GameObject _matchEndedPanel;
    [SerializeField] private GameObject _pausePanel;
    [SerializeField] private GameObject _scoresSidePanel;
    [SerializeField] private Image _scorerPanel;
    [SerializeField] private Color _redColor;
    [SerializeField] private Color _blueColor;
    [SerializeField] private Animator _scoresAnimator;
    [SerializeField] private Animator _matchAnimator;

    [Header("Score UI")] [SerializeField] private TextMeshProUGUI[] playerScoreText;
    [SerializeField] private TextMeshProUGUI[] aiScoreText;
    [SerializeField] private TextMeshProUGUI _scorerText;
    [SerializeField] private TextMeshProUGUI _serverText;
    [SerializeField] private TextMeshProUGUI _gameText;
    [SerializeField] private TextMeshProUGUI _matchOverText;
    [SerializeField] private TextMeshProUGUI _firstTo5Text;

    [Header("Buttons")] [SerializeField] private Button _rematchButton;
    [SerializeField] private Button _quitButton;

    [SerializeField] private float _messagesDuration = 1.5f;

    private const string PLAYER_SCORED_TEXT = "You scored!";
    private const string AI_SCORED_TEXT = "Opponent scored!";
    private const string VICTORY_TEXT = "VICTORY";
    private const string DEFEAT_TEXT = "DEFEAT";
    private const string PLAYER_SERVES_TEXT = "Your serve";
    private const string OPPONENT_SERVES_TEXT = "Opponent to serve";

    private const string SCORES_ANIMATOR_SHOWMAINPANEL = "showMainPanel";
    private const string SCORES_ANIMATOR_MATCHOVER = "matchOver";
    private const string MATCH_SHOWSERVER = "show";

    private const string GAME_FIRSTTO5 = "FIRST TO 5";
    private const string GAME_MATCHPOINT = "MATCH POINT";
    private const string GAME_TIEBREAK = "TIE BREAK";

    private bool _firstRally = true;

    private void Awake()
    {
        SetUpListeners();

        _matchEndedPanel.SetActive(false);
        _scoresSidePanel.SetActive(false);
        _pausePanel.SetActive(false);
        _gameText.gameObject.SetActive(false);
        _matchOverText.gameObject.SetActive(false);
        _scorerText.text = "";
        _firstTo5Text.text = "";
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            TogglePause();
    }

    private void TogglePause()
    {
        if (_matchEndedPanel.activeSelf) return;

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

    private void OnRematchClicked()
    {
        _firstRally = true;
        foreach (var t in playerScoreText)
            t.text = "0";
        foreach (var t in aiScoreText)
            t.text = "0";
        _matchController.RestartGame();
        _matchEndedPanel.SetActive(false);
        _firstTo5Text.gameObject.SetActive(true);
    }

    private void SetUpListeners()
    {
        _matchController.OnBallServed += OnBallServed;
        _matchController.OnServerAnnounced += OnServerAnnounced;
        _matchController.OnPointWon += OnPointWon;
        _matchController.OnRallyStarted += OnRallyStarted;
        _matchController.OnMatchOver += OnMatchOver;
        _matchController.OnTieBreak += OnTieBreak;
        _matchController.OnMatchPoint += OnMatchPoint;

        _rematchButton.onClick.AddListener(OnRematchClicked);
        _quitButton.onClick.AddListener(OnQuitClicked);
    }

    private void OnTieBreak()
    {
        _gameText.text = GAME_TIEBREAK;
        StartCoroutine(TextPop(_gameText));
    }

    private void OnMatchPoint()
    {
        _gameText.text = GAME_MATCHPOINT;
        StartCoroutine(TextPop(_gameText));
    }

    private void OnMatchOver(MatchController.Side winner)
    {
        _matchOverText.text = winner == MatchController.Side.Player ? VICTORY_TEXT : DEFEAT_TEXT;
        _scoresAnimator.SetTrigger(SCORES_ANIMATOR_MATCHOVER);
        StartCoroutine(TextPop(_matchOverText));
    }

    private void OnRallyStarted()
    {
        if (_firstRally)
        {
            StartCoroutine(TypeText(_firstTo5Text, GAME_FIRSTTO5));
            _firstRally = false;
        }

        _scoresAnimator.SetBool(SCORES_ANIMATOR_SHOWMAINPANEL, true);
        _matchAnimator.SetTrigger(MATCH_SHOWSERVER);
    }

    private void OnPointWon(MatchController.Side scorerSide)
    {
        var newText = scorerSide.Equals(MatchController.Side.Player) ? PLAYER_SCORED_TEXT : AI_SCORED_TEXT;
        _scorerText.text = newText;

        StartCoroutine(TypeText(_scorerText, newText));
        StartCoroutine(UpdateScore(scorerSide, .5f));
    }

    private void OnServerAnnounced(MatchController.Side serverSide)
    {
        _serverText.text = serverSide.Equals(MatchController.Side.Player) ? PLAYER_SERVES_TEXT : OPPONENT_SERVES_TEXT;
        _scorerPanel.color = serverSide.Equals(MatchController.Side.Player) ? _redColor : _blueColor;
    }

    private void OnBallServed(bool ballServed)
    {
        if (!ballServed) return;
        _scoresAnimator.SetBool(SCORES_ANIMATOR_SHOWMAINPANEL, false);
        _scorerText.text = "";
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
        var texts = scoreSide == MatchController.Side.Player ? playerScoreText : aiScoreText;
        var score = scoreSide == MatchController.Side.Player
            ? _matchController.playerScore.ToString()
            : _matchController.aiScore.ToString();
        StartCoroutine(AnimateScore(texts[0], score));
        texts[1].text = score;
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
        t = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            rect.anchoredPosition = Vector2.Lerp(topPos, originalPos, t / duration);
            yield return null;
        }

        rect.anchoredPosition = originalPos;
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
        _matchController.OnBallServed -= OnBallServed;
        _matchController.OnServerAnnounced -= OnServerAnnounced;
        _matchController.OnPointWon -= OnPointWon;
        _matchController.OnRallyStarted -= OnRallyStarted;
        _matchController.OnMatchOver -= OnMatchOver;
        _matchController.OnTieBreak -= OnTieBreak;
        _matchController.OnMatchPoint -= OnMatchPoint;
    }
}