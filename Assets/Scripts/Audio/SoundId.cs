/// <summary>
/// Every sound the game can ask for. Code names the moment, never the clip: what
/// it sounds like is decided in the SoundBank asset, so swapping art is a drag and
/// drop and nothing here has to change.
/// </summary>
public enum SoundId
{
    None = 0,

    // Menus
    ButtonHover = 10,
    ButtonClick = 11,
    ButtonBack = 12,
    ButtonApply = 13,
    SliderTick = 14,
    AvatarSelect = 15,

    // Start of a match
    CountdownTick = 20,
    CountdownGo = 21,

    // Rally
    PaddleHit = 30,
    TableBounce = 31,
    NetHit = 32,

    // Score
    PointWon = 40,
    PointLost = 41,
    MatchPoint = 42,

    // End of a match
    Victory = 50,
    Defeat = 51,
}
