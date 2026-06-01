public class RallyState : BaseMatchState
{
    public RallyState(MatchController match) : base(match)
    {
    }

    public override void OnBounce(MatchController.Side side)
    {
        Match.bounceCount++;
        Match.ballBounced = true;
        Match.lastBounceSide = side;

        if (Match.bounceCount >= 2 || side != Match.currentTurn)
        {
            Match.TransitionTo(new PointScoredState(Match, GetOpponent(side)));
        }
    }

    public override void OnHit(MatchController.Side side)
    {
        if (!Match.ballBounced)
        {
            Match.TransitionTo(new PointScoredState(Match, GetOpponent(side)));
            return;
        }

        Match.lastHitter = side;
        Match.currentTurn = GetOpponent(side);
        Match.ballBounced = false;
        Match.bounceCount = 0;
    }

    public override void OnBallOut()
    {
        MatchController.Side winner;

        if (!Match.ballBounced)
            winner = GetOpponent(Match.lastHitter);
        else
            winner = Match.lastHitter;

        Match.TransitionTo(new PointScoredState(Match, winner));
    }
}