namespace Shared;

public class NewTaskCreated : Event
{
    public Guid TaskId { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public TesTaskStatus Status { get; set; }
    public Guid Assigned { get; set; }

    public NewTaskCreated(TesTask newTask) 
        : base()
    {
        TaskId = newTask.Id;
        Name = newTask.Name;
        Description = newTask.Description;
        Status = newTask.Status;
        Assigned = newTask.Assigned;
    }
}