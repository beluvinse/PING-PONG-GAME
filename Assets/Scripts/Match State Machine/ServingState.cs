public class ServingState : BaseMatchState
{
    public ServingState(MatchController match) : base(match)
    {
    }

    public override void Enter()
    {
        Match.OnServerAnnounced?.Invoke(Match.server);
        Match.OnRallyStarted?.Invoke();
        Match.lastHitter = Match.server;
        Match.currentTurn = GetOpponent(Match.server);
        Match.ballBounced = false;
        Match.ballServed = false;
        Match.bounceCount = 0;
        Match.StartServeDelay(2f, () => Match.OnBallServed?.Invoke(false));
    }

    public override void OnBounce(MatchController.Side side)
    {
        if (side != Match.server)
        {
            Match.TransitionTo(new PointScoredState(Match, GetOpponent(side)));
            return;
        }

        Match.ballBounced = false;
        Match.bounceCount = 0;
        Match.TransitionTo(new RallyState(Match));
    }
    
    public override void OnBallOut()
    {
        Match.TransitionTo(new PointScoredState(Match, GetOpponent(Match.lastHitter)));
    }
}