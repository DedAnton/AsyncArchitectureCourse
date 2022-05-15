namespace Shared;

public class TaskAssigned : Event
{
    public Guid TaskId { get; }
    public Guid Assigned { get; }

    public TaskAssigned(Guid taskId, Guid assigned)
        : base()
    {
        TaskId = taskId;
        Assigned = assigned;
    }
}