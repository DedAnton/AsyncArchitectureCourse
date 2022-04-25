using LinqToDB.Mapping;

namespace Shared;

[Table(Name = "Task")]
public class TesTask
{
    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();
    [Column, NotNull]
    public string Name { get; set; } = "";
    [Column, NotNull]
    public string Description { get; set; } = "";
    [Column, NotNull]
    public TaskStatus Status { get; set; }
    [Column, NotNull]
    public Guid Assigned { get; set; }
    [Column, NotNull]
    public float Deposit { get; set; }
    [Column, NotNull]
    public float Fee { get; set; }
}

public enum TaskStatus
{
    InProgress,
    Done
}