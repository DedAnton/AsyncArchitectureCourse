var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKafka(kafka => kafka
    .UseConsoleLog()
    .AddCluster(x => x
        .WithBrokers(new[] { "localhost:9092" })
        // в реальном проекте я бы разделил CUD и бизнес события по разным топикам
        .AddProducer("taskProducer", "tasks")
        .AddConsumer("users", "tasksService", typeof(UserEventsHandler)) 
    )
);
builder.Services.AddMyAuthorization();
builder.Services.AddMySwagger();

var app = builder.Build();

app.UseKafka(app.Lifetime);
app.UseMyAuthorization();
app.UseMySwagger();

app.MapGet("/tasks", [Authorize(Roles = "Worker")] async (ClaimsPrincipal user) =>
{
    using var db = new DbTasks();
    var userId = user.GetId();
    var task = await db.Tasks
        .Where(x => x.Assigned == userId)
        .Select(x => new { x.Id, x.JiraId, x.Name, x.Description, x.Status, x.Assigned })
        .ToListAsync();
    return Results.Ok(task);
});

app.MapPost("/tasks", [Authorize] async (TesTask newTask) =>
{
    using var db = new DbTasks();
    if (newTask.Name.Contains('[') || newTask.Name.Contains(']'))
    {
        Results.BadRequest("do not use [jira-id] in name, use property jiraId");
    }
    if (string.IsNullOrWhiteSpace(newTask.JiraId))
    {
        Results.BadRequest("jiraId must be specified");
    }
    newTask = new TesTask { Id = newTask.Id, JiraId = newTask.JiraId, Name = newTask.Name, Description = newTask.Description, Status = TesTaskStatus.InProgress, Assigned = GetRandomWorker() };
    await db.InsertAsync(newTask);
    await ProduceEventAsync(new NewTaskCreatedV2(newTask));
    await ProduceEventAsync(new TaskAssigned(newTask.Id, newTask.Assigned));
    await ProduceEventAsync(new StreamingEvent<TesTask>(newTask));

    return Results.Ok();
});

app.MapPost("/tasks/{taskId}/complete", [Authorize(Roles = "Worker")] 
    async (ClaimsPrincipal user, Guid taskId) =>
{
    using var db = new DbTasks();

    var task = await db.Tasks.FirstOrDefaultAsync(x => x.Id == taskId);
    if (task?.Assigned != user.GetId())
    {
        return Results.NotFound(taskId);
    }
    if (task.Status == TesTaskStatus.Completed)
    {
        return Results.BadRequest($"Task {taskId} already completed");
    }

    await db.Tasks
        .Where(x => x.Id == taskId)
        .Set(x => x.Status, TesTaskStatus.Completed)
        .UpdateAsync();
    await ProduceEventAsync(new TaskCompleted(taskId));

    task.Status = TesTaskStatus.Completed;
    await ProduceEventAsync(new StreamingEvent<TesTask>(task));

    return Results.Ok();
});

app.MapPost("/tasks/shuffle", [Authorize(Roles = "Manager,Admin")] async () =>
{
    using var db = new DbTasks();

    var tasks = await db.Tasks.Where(x => x.Status == TesTaskStatus.InProgress).ToListAsync();
    foreach(var task in tasks)
    {
        var assign = GetRandomWorker();
        if (task.Assigned == assign)
        {
            continue;
        }
        task.Assigned = assign;
        await db.Tasks
            .Where(x => x.Id == task.Id)
            .Set(x => x.Assigned, task.Assigned)
            .UpdateAsync();
        await ProduceEventAsync(new TaskAssigned(task.Id, assign));
        await ProduceEventAsync(new StreamingEvent<TesTask>(task));
    }
    await ProduceEventAsync(new TasksShuffled());

    return Results.Ok();
});

Guid GetRandomWorker()
{
    using var db = new DbTasks();
    var workers = db.TaskUser.Where(x => x.Role == "Worker").Select(x => x.Id).ToList();
    if (workers.Count == 0)
    {
        throw new Exception("Worker was not found");
    }
    var randomWorker = workers[new Random().Next(0, workers.Count)];

    return randomWorker;
}

async Task ProduceEventAsync<T>(T @event) where T : Event
{
    var producers = app!.Services.GetRequiredService<IProducerAccessor>();
    await producers["taskProducer"].ProduceAsync(DateTime.UtcNow.Ticks.ToString(), @event);
}

app.Run($"http://localhost:5002");

public class UserEventsHandler : IMessageHandler<StreamingEvent<User>>
{
    public async Task Handle(IMessageContext context, StreamingEvent<User> userUpdate)
    {
        using var db = new DbTasks();
        await db.TaskUser.InsertOrUpdateAsync(
            () => new User { Id = userUpdate.Data.Id, Username = userUpdate.Data.Username, Role = userUpdate.Data.Role , IsDeleted = userUpdate.Data.IsDeleted},
            x => new User { Id = x.Id, Username = userUpdate.Data.Username, Role = userUpdate.Data.Role, IsDeleted = userUpdate.Data.IsDeleted });
    }
}

public class DbTasks : LinqToDB.Data.DataConnection
{
    public DbTasks() : base(ProviderName.SqlServer2019, @"Server=ANTON-PC\SQLEXPRESS;Database=Tasks;Trusted_Connection=True;TrustServerCertificate=True;Enlist=False;", CreateMappingSchema()) { }

    private static MappingSchema CreateMappingSchema()
    {
        var mappingSchema = new MappingSchema();
        var builder = mappingSchema.GetFluentMappingBuilder();

        builder.Entity<User>()
            .HasTableName("User")
            .HasPrimaryKey(x => x.Id)
            .Ignore(x => x.Password);
        builder.Entity<TesTask>()
            .HasTableName("Task")
            .HasPrimaryKey(x => x.Id)
            .Ignore(x => x.Reward)
            .Ignore(x => x.Fee);

        return mappingSchema;
    }

    public ITable<TesTask> Tasks => this.GetTable<TesTask>();
    public ITable<User> TaskUser => this.GetTable<User>();
}