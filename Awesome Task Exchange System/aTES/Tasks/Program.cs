using LinqToDB;
using Shared;
using LinqToDB.Mapping;
using KafkaFlow.TypedHandler;
using KafkaFlow;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services
    .AddKafka("broker:9092")
        .AddConsumer("users", "TasksService")
            .AddHandler<UserHandler>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.Services.UseKafka(app.Lifetime);

app.Run($"http://localhost:5002");

public class UserHandler : IMessageHandler<User>
{
    public async Task Handle(IMessageContext context, User userMsg)
    {
        using var db = new DbTasks();
        await db.TaskUser.InsertOrUpdateAsync(
            () => new TaskUser { Id = userMsg.Id, Username = userMsg.Username }, 
            x => new TaskUser { Id = x.Id, Username = userMsg.Username });
    }
}

public class DbTasks : LinqToDB.Data.DataConnection
{
    public DbTasks() : base(@"Server=ANTON-PC\SQLEXPRESS;Database=Tasks;Trusted_Connection=True;Enlist=False;") { }

    public ITable<TesTask> Task => GetTable<TesTask>();
    public ITable<TaskUser> TaskUser => GetTable<TaskUser>();
}

[Table(Name = "TaskUser")]
public class TaskUser
{
    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();
    [Column, NotNull]
    public string Username { get; set; } = "";
}

public enum TaskStatus
{
    InProgress,
    Done
}