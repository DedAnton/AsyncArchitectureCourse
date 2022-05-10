namespace Shared;

public class TaskCompleted : Event
{
    public Guid TaskId { get; }

    public TaskCompleted(Guid taskId)
        :base()
    {
        TaskId = taskId;
    }
}