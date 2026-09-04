namespace ECommerce.Shared.Kernel.Primitives;

/// <summary>Time as a dependency, so staleness rules are testable without waiting.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
