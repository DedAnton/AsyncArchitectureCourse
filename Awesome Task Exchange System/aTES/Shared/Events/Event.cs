namespace Shared;

public abstract class Event
{
    protected Event()
    {
        Id = Guid.NewGuid();
        Timestamp = DateTime.UtcNow.Ticks;
    }

    public Guid Id { get; }
    public long Timestamp { get; }
}