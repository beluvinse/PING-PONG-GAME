using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MatchController _matchController;

    [SerializeField] private GameObject _scoresMainPanel;
    [SerializeField] private GameObject _scoresSidePanel;
    [SerializeField] private GameObject _matchEndedPanel;
    [SerializeField] private GameObject _pausePanel;
    
    [Header("Score UI")]
    [SerializeField] private TextMeshProUGUI[] playerScoreText;
    [SerializeField] private TextMeshProUGUI[] aiScoreText;
    [SerializeField] private TextMeshProUGUI _scorerText;
    [SerializeField] private TextMeshProUGUI _serverText;
    [SerializeField] private TextMeshProUGUI _gameText;
    [SerializeField] private TextMeshProUGUI _matchOverText;

    [Header("Buttons")]
    [SerializeField] private Button _rematchButton;
    [SerializeField] private Button _quitButton;
    
    private const string PLAYER_SCORED_TEXT = "You scored!";
    private const string AI_SCORED_TEXT = "Opponent scored!";
    
    private void Awake()
    {
        Cursor.visible = false;

        _matchController.OnBallServed += OnBallServed;
        _matchController.OnServerAnnounced += OnServerAnnounced;
        _matchController.OnPointWon += OnPointWon;
        _matchController.OnRallyStarted += OnRallyStarted;
        _matchController.OnMatchOver += OnMatchOver;
        
        _rematchButton.onClick.AddListener(OnRematchClicked);
        _quitButton.onClick.AddListener(OnQuitClicked);
        
        _scoresMainPanel.SetActive(false);
        _scoresSidePanel.SetActive(false);
        _matchEndedPanel.SetActive(false);
        _pausePanel.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            TogglePause();
    }

    private void TogglePause()
    {
        _pausePanel.SetActive(!_pausePanel.activeSelf);
        Time.timeScale = _pausePanel.activeSelf ? 0 : 1;
    }

    private void OnQuitClicked()
    {
        UnityEditor.EditorApplication.isPlaying = false;
    }

    private void OnRematchClicked()
    {
        Cursor.visible = false;
        _matchController.playerScore = 0;
        _matchController.aiScore = 0;
        UpdateScoreUI();
        SetGameText();
        _matchController.RestartGame();
        _matchEndedPanel.SetActive(false);
    }

    private void OnMatchOver(MatchController.Side winner)
    {
        Cursor.visible = true;
        _matchOverText.text = winner == MatchController.Side.Player ? "VICTORY" : "DEFEAT";
        _matchEndedPanel.SetActive(true);
    }

    private void OnRallyStarted()
    {
        _scoresMainPanel.SetActive(true);
        _scoresSidePanel.SetActive(false);
    }

    private void OnPointWon(MatchController.Side scorerSide)
    {
        var newText = scorerSide.Equals(MatchController.Side.Player) ? PLAYER_SCORED_TEXT : AI_SCORED_TEXT;
        _scorerText.text = newText;
        _scorerText.gameObject.SetActive(true);
        StartCoroutine(HideScorerText());
        UpdateScoreUI();
    }

    private IEnumerator HideScorerText()
    {
        yield return new WaitForSeconds(1.5f);
        _scorerText.gameObject.SetActive(false);
    }

    private void OnServerAnnounced(MatchController.Side serverSide)
    {
        _serverText.text = serverSide.Equals(MatchController.Side.Player) ? "Your serve" : "Opponent to serve";
    }

    private void OnBallServed(bool ballServed)
    {
        if (!ballServed) return;
        _scoresMainPanel.SetActive(false);
        _scoresSidePanel.SetActive(false);
        _scorerText.gameObject.SetActive(false);
    }

    private void UpdateScoreUI()
    {
        foreach (var t in playerScoreText)
        {
            t.text = _matchController.playerScore.ToString();
        }
        foreach (var t in aiScoreText)
        {
            t.text = _matchController.aiScore.ToString();
        }
    }

    private void SetGameText()
    {
        _gameText.text = "FIRST TO 5";
    }

    private void OnDestroy()
    {
        _matchController.OnBallServed -= OnBallServed;
        _matchController.OnServerAnnounced -= OnServerAnnounced;
        _matchController.OnPointWon -= OnPointWon;
    }
}
