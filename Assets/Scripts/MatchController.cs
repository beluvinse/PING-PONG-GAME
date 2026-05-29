using System;
using UnityEngine;

public class MatchController : MonoBehaviour
{
    public enum Side
    {
        Player,
        AI
    }

    [Header("Score")]
    public int playerScore;
    public int aiScore;

    [Header("State")]
    public Side currentTurn;
    public Side lastHitter;

    public bool ballBounced;
    public bool ballServed;
    public bool waitingServeBounce;

    public int bounceCount;
    public Side server;

    public void StartRally(Side newServer)
    {
        server = newServer;
        
        lastHitter = newServer;

        currentTurn =
            newServer == Side.Player
                ? Side.AI
                : Side.Player;

        ballBounced = false;

        bounceCount = 0;
        waitingServeBounce = true;
        ballServed = false;
    }

    private void Start()
    {
        StartRally(Side.Player);
    }

    public void RegisterBounce(Side side)
    {
        bounceCount++;

        ballBounced = true;

        // =========================
        // SERVE FIRST BOUNCE
        // =========================

        if (waitingServeBounce)
        {
            // el primer pique TIENE que ser
            // en el lado del server

            if (side != server)
            {
                Debug.Log("SIDE BOUNCED ON " + side);
                Debug.Log("SERVER IS " + server);
                Side winner =
                    side == Side.Player
                        ? Side.AI
                        : Side.Player;

                AwardPoint(winner);

                return;
            }

            Debug.Log("VALID SERVE BOUNCE");

            waitingServeBounce = false;
            bounceCount = 0;
            return;
        }

        // =========================
        // NORMAL RALLY
        // =========================

        if (side != currentTurn)
        {
            Side winner =
                side == Side.Player
                    ? Side.AI
                    : Side.Player;

            AwardPoint(winner);

            return;
        }

        if (bounceCount >= 2)
        {
            Side winner =
                side == Side.Player
                    ? Side.AI
                    : Side.Player;

            AwardPoint(winner);
        }
    }
    
    public bool CanHit(Side side)
    {
        if (!ballServed)
            return true;
        
        // no puede pegar dos veces seguidas
        if (side == lastHitter)
            return false;

        // tiene que haber picado primero
        if (!ballBounced)
            return false;

        // tiene que picar en su lado
        if (currentTurn != side)
            return false;

        return true;
    }
    

    public void RegisterHit(Side side)
    {
        lastHitter = side;

        currentTurn = side == Side.Player ? Side.AI : Side.Player;

        bounceCount = 0;
        ballBounced = false;

        Debug.Log(
            side +
            " HIT BALL"
        );
    }

    // =========================
    // POINT
    // =========================

    void AwardPoint(Side winner)
    {
        if (winner == Side.Player)
        {
            playerScore++;
        }
        else
        {
            aiScore++;
        }

        Debug.Log(
            "POINT FOR: " +
            winner
        );

        Debug.Log(
            "PLAYER: " +
            playerScore +
            " | AI: " +
            aiScore
        );

        ResetRally(winner);
    }
    
    void ResetRally(Side server)
    {
        bounceCount = 0;

        ballBounced = false;

        lastHitter = server;

        currentTurn =
            server == Side.Player
                ? Side.AI
                : Side.Player;
    }

    public void SetBallServed()
    {
        ballServed = true;
    }
}