namespace Shared;

public class TaskEstimated : Event
{
    public Guid TaskId { get; }
    public float Reward { get; }
    public float Fee { get; }

    public TaskEstimated(Guid taskId, float reward, float fee)
        : base()
    {
        TaskId = taskId;
        Reward = reward;
        Fee = fee;
    }
}