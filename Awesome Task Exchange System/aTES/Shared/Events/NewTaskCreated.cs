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

//План миграции: 
//1. обновить нугет пакет с ивентом (в моем случае проект Shared)
//2. обновить код консьюмера, чтобы он обрабатывал и старую и новую версию ивента
//3. обновить продьюсерра, чтобы он слал вторую версию
//4. убедиться что все работает нормально
//5. обновить код консьюмера, чтобы он обрабатывал только новую версию ивента
//6. вырезать старый ивент из нугет пакета

// я остановился на промежуточном варианте, когда продьюсер шлет новую версию, а консьюмер принимает обе (просто чтобы понятнее было)

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