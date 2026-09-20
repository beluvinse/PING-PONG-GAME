using System;
using System.Collections;
using UnityEngine;

public class MatchController : MonoBehaviour
{
    public enum Side { Player, AI }

    [Header("Score")]
    public int playerScore;
    public int aiScore;

    [Header("State")]
    public Side currentTurn;
    public Side lastHitter;
    public Side server;

    public bool ballBounced;
    public bool ballServed;
    public int bounceCount;
    public Side lastBounceSide;
    public int servesDone;

    public Action<Side> OnPointWon;
    public Action<Side> OnServerAnnounced;
    public Action<bool> OnBallServed;
    public Action OnRallyStarted;
    public Action OnTieBreak;
    public Action OnMatchPoint;

    /// <summary>Raised when a match begins: auto-start, rematch or replay from the end screen.</summary>
    public Action OnMatchStarted;

    /// <summary>How many matches have begun in this scene. Lets late listeners tell they missed one.</summary>
    public int MatchesStarted { get; private set; }

    public bool IsRallyActive => _currentState is RallyState;

    [Header("Start")]
    [Tooltip("Serves as soon as the scene loads, instead of waiting for the Play button.")]
    [SerializeField] private bool _autoStart = true;
    [Tooltip("Optional. The first serve waits for this countdown, so 'GO!' and the ball land together.")]
    [SerializeField] private StartCountdown _startCountdown;
    [Tooltip("Pause between a serve being announced and the ball being placed.")]
    [SerializeField] private float _serveDelay = 3f;

    /// <summary>False leaves the old Play/Quit panel up instead of serving.</summary>
    public bool AutoStart => _autoStart;

    /// <summary>The countdown this match waits for, so the UI can follow it. Can be null.</summary>
    public StartCountdown Countdown => _startCountdown;

    private BaseMatchState _currentState;
    private bool _countdownPending;

    private void Start()
    {
        if (!_autoStart) return;

        RestartGame();
    }

    /// <summary>
    /// Pause before the ball is placed. The very first serve of an auto-started
    /// match stretches to cover the countdown; every serve after it is normal.
    /// </summary>
    public float NextServeDelay()
    {
        if (!_countdownPending) return _serveDelay;

        _countdownPending = false;
        return Mathf.Max(_serveDelay, _startCountdown.TotalDuration);
    }

    public void RestartGame()
    {
        server = Side.Player;
        playerScore = 0;
        aiScore = 0;
        servesDone = 0;

        // Before the serve state, so anything set up for the new match (the
        // opponent's avatar) is in place when the serve is announced.
        MatchesStarted++;
        OnMatchStarted?.Invoke();

        // The first serve of a match waits for the countdown. The first match of
        // the scene already has one running, held back until the scene wipe opens;
        // every match after it - a restart, a rematch - has to ask for a new one.
        _countdownPending = _startCountdown != null;
        if (MatchesStarted > 1 && _countdownPending) _startCountdown.Play();

        TransitionTo(new ServingState(this));
    }

    /// <summary>Aborts the current match and returns to the pre-match idle state (main menu).</summary>
    public void StopMatch()
    {
        StopAllCoroutines();
        server = Side.Player;
        playerScore = 0;
        aiScore = 0;
        servesDone = 0;
        ballServed = false;
        ballBounced = false;
        bounceCount = 0;
        isServeReady = false;
        TransitionTo(new IdleState(this));
    }
    

    public void TransitionTo(BaseMatchState newState)
    {
        _currentState?.Exit();
        _currentState = newState;
        _currentState.Enter();
    }

    public bool IsServer(Side side) => side == server;

    public void SetBallServed()
    {
        ballServed = true;
        OnBallServed?.Invoke(true);
    }

    public bool CanHit(Side side)
    {
        if (!ballServed && !isServeReady) return false;
        if (!ballServed) return true;
        if (side == lastHitter) return false;
        if (!ballBounced) return false;
        if (lastBounceSide != side) return false;
        if (currentTurn != side) return false;
        return true;
    }

    public void RegisterBounce(Side side) => _currentState?.OnBounce(side);
    public void RegisterHit(Side side) => _currentState?.OnHit(side);
    public void RegisterBallOut() => _currentState?.OnBallOut();

    private bool isServeReady;
    
    public void StartDelay(float delay, Action onComplete)
    {
        StartCoroutine(DelayRoutine(delay, onComplete));
    }

    private IEnumerator DelayRoutine(float delay, Action onComplete)
    {
        yield return new WaitForSeconds(delay);
        onComplete?.Invoke();
    }

    public void StartServeDelay(float delay, Action onReady)
    {
        isServeReady = false;
        StartDelay(delay, () => { isServeReady = true; onReady?.Invoke(); });
    }

    private IEnumerator MatchOverRoutine(float delay, Action onReady)
    {
        yield return new WaitForSeconds(delay);
        onReady?.Invoke();
    }

    public Action<Side> OnMatchOver;

    public bool IsMatchOver()
    {
        const int pointsToWin = 5;

        var playerReached = playerScore >= pointsToWin;

        var aiReached = aiScore >= pointsToWin;

        if (!playerReached && !aiReached)
            return false;

        return Mathf.Abs(playerScore - aiScore) >= 2;
    }
    
    public void CheckSpecialScoreStates()
    {
        if (playerScore == 4 && aiScore == 4)
            OnTieBreak?.Invoke();
        else if (IsMatchPoint(playerScore, aiScore) || IsMatchPoint(aiScore, playerScore))
            OnMatchPoint?.Invoke();
    }
    
    private bool IsMatchPoint(int myScore, int opponentScore)
    {
        if (myScore < 4) return false;
        if (myScore == 4 && opponentScore < 4) return true;
        return myScore == opponentScore + 1;
    }
}