namespace Shared;

public class StreamingEvent<T> : Event
{
    public T Data { get; }

    public StreamingEvent(T data)
        :base()
    {
        Data = data;
    }
}