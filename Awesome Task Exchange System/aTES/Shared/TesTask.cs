using LinqToDB.Mapping;

namespace Shared;

//использую совмещенную модель просто чтобы меньше кода писать
public class TesTask
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public TesTaskStatus Status { get; set; }
    public Guid Assigned { get; set; }
    public float? Reward { get; set; }
    public float? Fee { get; set; }
}

public enum TesTaskStatus
{
    InProgress,
    Completed
}