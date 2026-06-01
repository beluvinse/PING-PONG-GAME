public abstract class BaseMatchState
{
    protected MatchController Match;

    protected BaseMatchState(MatchController match)
    {
        Match = match;
    }

    public virtual void Enter() { }
    public void Exit() { }
    public virtual void OnBounce(MatchController.Side side) { }
    public virtual void OnHit(MatchController.Side side) { }
    public virtual void OnBallOut() { }

    protected MatchController.Side GetOpponent(MatchController.Side side) =>
        side == MatchController.Side.Player
            ? MatchController.Side.AI
            : MatchController.Side.Player;
}