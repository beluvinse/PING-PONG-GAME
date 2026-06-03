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

    public bool IsRallyActive => _currentState is RallyState;
    
    private BaseMatchState _currentState;
    
    public void RestartGame()
    {
        server = Side.Player;
        playerScore = 0;
        aiScore = 0;
        servesDone = 0;
        TransitionTo(new ServingState(this));
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

    // public void StartServeDelay(float delay, Action onReady)
    // {
    //     isServeReady = false;
    //     StartCoroutine(ServeDelayRoutine(delay, onReady));
    // }
    //
    // private IEnumerator ServeDelayRoutine(float delay, Action onReady)
    // {
    //     yield return new WaitForSeconds(delay);
    //     isServeReady = true;
    //     onReady?.Invoke();
    // }
    //
    // public void StartDelayMatchOver(float delay, Action onReady)
    // {
    //     StartCoroutine(MatchOverRoutine(delay, onReady));
    // }
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