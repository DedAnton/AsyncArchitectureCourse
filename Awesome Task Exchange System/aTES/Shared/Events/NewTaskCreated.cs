namespace Shared;

public class NewTaskCreated : Event
{
    public Guid TaskId { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public TesTaskStatus Status { get; set; }
    public Guid Assigned { get; set; }

    public NewTaskCreated() : base() { }

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

//���� ��������: 
//1. �������� ����� ����� � ������� (� ���� ������ ������ Shared)
//2. �������� ��� ����������, ����� �� ����������� � ������ � ����� ������ ������
//3. �������� �����������, ����� �� ���� ������ ������
//4. ��������� ��� ��� �������� ���������
//5. �������� ��� ����������, ����� �� ����������� ������ ����� ������ ������
//6. �������� ������ ����� �� ����� ������

// � ����������� �� ������������� ��������, ����� ��������� ���� ����� ������, � ��������� ��������� ��� (������ ����� �������� ����)

public class NewTaskCreatedV2 : Event
{
    public Guid TaskId { get; set; }
    public string JiraId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public TesTaskStatus Status { get; set; }
    public Guid Assigned { get; set; }

    public NewTaskCreatedV2() : base() { }

    public NewTaskCreatedV2(TesTask newTask)
        : base()
    {
        TaskId = newTask.Id;
        JiraId = newTask.JiraId;
        Name = newTask.Name;
        Description = newTask.Description;
        Status = newTask.Status;
        Assigned = newTask.Assigned;
    }
}