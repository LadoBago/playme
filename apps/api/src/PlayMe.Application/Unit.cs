namespace PlayMe.Application;

/// <summary>Marker for handlers that return no value but can still fail.</summary>
public readonly record struct Unit
{
    public static Unit Value => default;
}
