// Shared environment vocabulary and the immutable snapshot broadcast through the
// single EnvironmentManager channel. Every responsive entity receives the same
// snapshot and derives its own appearance from it.
public enum Season
{
    Spring,
    Summer,
    Autumn,
    Winter
}

public enum Weather
{
    Clear,
    Cloudy,
    Rain
}

public enum TimeOfDay
{
    Day,
    Night
}

public struct EnvironmentSnapshot
{
    public Weather Weather;
    public TimeOfDay TimeOfDay;
    public Season Season;

    // Winter rain reads as snow for any entity that cares about precipitation type
    public bool IsSnowing => Season == Season.Winter && Weather == Weather.Rain;
    public bool IsRaining => Season != Season.Winter && Weather == Weather.Rain;
}
