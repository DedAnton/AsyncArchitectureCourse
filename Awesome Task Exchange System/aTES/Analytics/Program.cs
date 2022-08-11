var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKafka(kafka => kafka
    .UseConsoleLog()
    .AddCluster(x => x
        .WithBrokers(new[] { "localhost:9092" })
        .AddConsumer("tasks", "analyticsService", typeof(TaskEventsHandler))
        .AddConsumer("transactions", "analyticsService", typeof(TransactionEventsHandler))
    )
);
builder.Services.AddMyAuthorization();
builder.Services.AddMySwagger();
var app = builder.Build();

app.UseKafka(app.Lifetime);
app.UseMyAuthorization();
app.UseMySwagger();

app.MapGet("/money/earned", [Authorize(Roles = "Admin, Accounter")] async () =>
{
    using var db = new DbAnalytics();
    var weekStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7));
    var earnedByDay = await db.TaskHistory
        .Where(x => x.Date >= weekStart)
        .GroupBy(x => x.Date)
        .Select(x => new { Date = x.Key, Earned = x.Sum(y => y.Debit - y.Credit) })
        .ToListAsync();

    return Results.Json(earnedByDay, JsonOptions.DateOnly);
});

app.MapGet("/money/earned/today", [Authorize(Roles = "Admin")] async () =>
{
    using var db = new DbAnalytics();
    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    var earned = await db.TaskHistory.Where(x => x.Date == today).SumAsync(x => x.Debit - x.Credit);

    return Results.Ok(new { EarnedToday = earned });
});

app.MapGet("/workers/balance/negative", [Authorize(Roles = "Admin")] async () =>
{
    using var db = new DbAnalytics();
    var negativeCount = await db.Transactions
        .GroupBy(x => x.User)
        .Where(x => x.Sum(y => y.Debit - y.Credit) < 0)
        .CountAsync();

    return Results.Ok(new { WorkersWithNegativeBalance = negativeCount });
});

app.MapGet("/tasks/expensiveness", [Authorize(Roles = "Admin")] async () =>
{
    using var db = new DbAnalytics();
    var weekStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7));
    var maxRewardByDay = await db.TaskHistory
        .Where(x => x.Date >= weekStart && x.Type == TaskHistoryType.Estimate)
        .GroupBy(x => x.Date)
        .Select(x => new { Date = x.Key, MaxReward = x.Max(x => x.Reward) })
        .ToListAsync();
    
    return Results.Json(maxRewardByDay, JsonOptions.DateOnly);
});

app.Run($"http://localhost:5004");

public class TaskEventsHandler : IMessageHandler<TaskEstimated>, IMessageHandler<TaskAssigned>, IMessageHandler<TaskCompleted>
{
    public async Task Handle(IMessageContext context, TaskEstimated message)
    {
        using var db = new DbAnalytics();
        var date = DateOnly.FromDateTime(new DateTime(message.Timestamp, DateTimeKind.Utc));
        await db.TaskHistory.InsertAsync(() => new TaskHistory { EventId = message.Id, Date = date, TaskId = message.TaskId, Type = TaskHistoryType.Estimate, Debit = 0, Credit = 0, Reward = message.Reward, Fee = message.Fee });
        await db.TaskHistory.Where(x => x.TaskId == message.TaskId && x.Type == TaskHistoryType.Assign)
            .Set(x => x.Debit, message.Fee)
            .UpdateAsync();
        await db.TaskHistory.Where(x => x.TaskId == message.TaskId && x.Type == TaskHistoryType.Complete)
            .Set(x => x.Credit, message.Reward)
            .UpdateAsync();
    }

    public async Task Handle(IMessageContext context, TaskAssigned message)
    {
        using var db = new DbAnalytics();
        var date = DateOnly.FromDateTime(new DateTime(message.Timestamp, DateTimeKind.Utc));
        var (reward, fee) = db.TaskHistory
            .Where(x => x.TaskId == message.TaskId && x.Type == TaskHistoryType.Estimate)
            .ToList()
            .Select(x => (x.Reward, x.Fee))
            .FirstOrDefault((0f, 0f));
        await db.TaskHistory.InsertAsync(() => new TaskHistory { EventId = message.Id, Date = date, TaskId = message.TaskId, Type = TaskHistoryType.Assign, Debit = fee, Credit = 0, Reward = reward, Fee = fee });
    }

    public async Task Handle(IMessageContext context, TaskCompleted message)
    {
        using var db = new DbAnalytics();
        var date = DateOnly.FromDateTime(new DateTime(message.Timestamp, DateTimeKind.Utc));
        var (reward, fee) = db.TaskHistory
            .Where(x => x.TaskId == message.TaskId && x.Type == TaskHistoryType.Estimate)
            .ToList()
            .Select(x => (x.Reward, x.Fee))
            .FirstOrDefault((0f, 0f));
        await db.TaskHistory.InsertAsync(() => new TaskHistory { EventId = message.Id, Date = date, TaskId = message.TaskId, Type = TaskHistoryType.Complete, Debit = 0, Credit = reward, Reward = reward, Fee = fee });
    }
}

public class TransactionEventsHandler : IMessageHandler<StreamingEvent<Transaction>>
{
    public async Task Handle(IMessageContext context, StreamingEvent<Transaction> transactionUpdate)
    {
        using var db = new DbAnalytics();
        await db.Transactions.InsertOrUpdateAsync(
            () => new Transaction { Id = transactionUpdate.Data.Id, User = transactionUpdate.Data.User, Reason = transactionUpdate.Data.Reason, Debit = transactionUpdate.Data.Debit, Credit = transactionUpdate.Data.Credit, CreatedAt = transactionUpdate.Data.CreatedAt },
            x => new Transaction { Id = x.Id, User = transactionUpdate.Data.User, Reason = transactionUpdate.Data.Reason, Debit = transactionUpdate.Data.Debit, Credit = transactionUpdate.Data.Credit, CreatedAt = transactionUpdate.Data.CreatedAt });
    }
}

public class TaskHistory
{
    public Guid EventId { get; set; }
    public DateOnly Date { get; set; }
    public Guid TaskId { get; set; }
    public TaskHistoryType Type { get; set; }
    public float Debit { get; set; }
    public float Credit { get; set; }
    public float Reward { get; set; }
    public float Fee { get; set; }
}

public enum TaskHistoryType
{
    Estimate,
    Assign,
    Complete
}

public class DbAnalytics : LinqToDB.Data.DataConnection
{
    public DbAnalytics() : base(ProviderName.SqlServer2019, @"Server=ANTON-PC\SQLEXPRESS;Database=Analytics;Trusted_Connection=True;TrustServerCertificate=True;Enlist=False;", CreateMappingSchema()) { }

    private static MappingSchema CreateMappingSchema()
    {
        var mappingSchema = new MappingSchema();
        var builder = mappingSchema.GetFluentMappingBuilder();

        builder.Entity<TaskHistory>()
            .HasTableName("TaskHistory")
            .HasPrimaryKey(x => x.EventId);
        builder.Entity<Transaction>()
            .HasTableName("Transaction")
            .HasPrimaryKey(x => x.Id);

        return mappingSchema;
    }

    public ITable<TaskHistory> TaskHistory => this.GetTable<TaskHistory>();
    public ITable<Transaction> Transactions => this.GetTable<Transaction>();
}