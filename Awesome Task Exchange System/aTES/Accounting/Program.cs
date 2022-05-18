var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKafka(kafka => kafka
    .UseConsoleLog()
    .AddCluster(x => x
        .WithBrokers(new[] { "localhost:9092" })
        .AddProducer("accountingTaskProducer", "tasks")
        .AddProducer("accountingTransactionProducer", "transactions")
        .AddConsumer("users", "accountingService", typeof(UserEventsHandler))
        .AddConsumer("tasks", "accountingService", typeof(TaskEventsHandler))
    )
);
builder.Services.AddMyAuthorization();
builder.Services.AddMySwagger();
builder.Services.AddHostedService<BillingCycleService>();

var app = builder.Build();

app.UseKafka(app.Lifetime);
app.UseMyAuthorization();
app.UseMySwagger();

app.MapGet("/balance", [Authorize(Roles = "Worker")] async (ClaimsPrincipal user) =>
{
    using var db = new DbAccounting();
    var userId = user.GetId();
    var userTransactions = await db.Transactions.Where(x => x.User == userId).ToListAsync();
    var balance = userTransactions.Sum(x => x.Debit - x.Credit);

    return Results.Ok(new { Balance = balance, Transactions = userTransactions });
});

app.Run($"http://localhost:5003");

public class UserEventsHandler : IMessageHandler<StreamingEvent<User>>
{
    public async Task Handle(IMessageContext context, StreamingEvent<User> userUpdate)
    {
        using var db = new DbAccounting();
        await db.TaskUsers.InsertOrUpdateAsync(
            () => new User { Id = userUpdate.Data.Id, Username = userUpdate.Data.Username, Role = userUpdate.Data.Role, IsDeleted = userUpdate.Data.IsDeleted },
            x => new User { Id = x.Id, Username = userUpdate.Data.Username, Role = userUpdate.Data.Role, IsDeleted = userUpdate.Data.IsDeleted });
    }
}

public class TaskEventsHandler : IMessageHandler<StreamingEvent<TesTask>>, IMessageHandler<NewTaskCreated>, IMessageHandler<NewTaskCreatedV2>, IMessageHandler<TaskAssigned>, IMessageHandler<TaskCompleted>
{
    private readonly IMessageProducer _taskProducer;
    private readonly IMessageProducer _transactionProducer;

    public TaskEventsHandler(IProducerAccessor producerAccessor)
    {
        _taskProducer = producerAccessor["accountingTaskProducer"];
        _transactionProducer = producerAccessor["accountingTransactionProducer"];
    }

    public async Task Handle(IMessageContext context, StreamingEvent<TesTask> taskUpdate)
    {
        using var db = new DbAccounting();
        await db.Tasks.InsertOrUpdateAsync(
            () => new TesTask { Id = taskUpdate.Data.Id, JiraId = taskUpdate.Data.JiraId, Name = taskUpdate.Data.Name, Description = taskUpdate.Data.Description, Status = taskUpdate.Data.Status, Assigned = taskUpdate.Data.Assigned },
            x => new TesTask { Id = x.Id, JiraId = taskUpdate.Data.JiraId, Name = taskUpdate.Data.Name, Description = taskUpdate.Data.Description, Status = taskUpdate.Data.Status, Assigned = taskUpdate.Data.Assigned });
    }

    public async Task Handle(IMessageContext context, NewTaskCreated message)
    {
        using var db = new DbAccounting();
        var random = new Random();
        var reward = random.Next(20, 41);
        var fee = random.Next(10, 21);
        await db.Tasks.InsertOrUpdateAsync(
            () => new TesTask { Id = message.TaskId, JiraId = "", Name = message.Name, Description = message.Description, Status = message.Status, Assigned = message.Assigned, Reward = reward, Fee = fee},
            x => new TesTask { Id = x.Id, Reward = reward, Fee = fee });
        await _taskProducer.ProduceAsync(DateTime.UtcNow.Ticks.ToString(), new TaskEstimated(message.TaskId, reward, fee));
    }

    public async Task Handle(IMessageContext context, NewTaskCreatedV2 message)
    {
        using var db = new DbAccounting();
        var random = new Random();
        var reward = random.Next(20, 41);
        var fee = random.Next(10, 21);
        await db.Tasks.InsertOrUpdateAsync(
            () => new TesTask { Id = message.TaskId, JiraId = message.JiraId, Name = message.Name, Description = message.Description, Status = message.Status, Assigned = message.Assigned, Reward = reward, Fee = fee },
            x => new TesTask { Id = x.Id, Reward = reward, Fee = fee });
        await _taskProducer.ProduceAsync(DateTime.UtcNow.Ticks.ToString(), new TaskEstimated(message.TaskId, reward, fee));
    }

    public async Task Handle(IMessageContext context, TaskAssigned message)
    {
        using var db = new DbAccounting();
        await db.Tasks.InsertOrUpdateAsync(
            () => new TesTask { Id = message.TaskId, Assigned = message.Assigned, Status = TesTaskStatus.InProgress },
            x => new TesTask { Id = x.Id, Assigned = message.Assigned });
        await ApplyFeeTransaction(message.Assigned, message.TaskId);
    }

    public async Task Handle(IMessageContext context, TaskCompleted message)
    {
        using var db = new DbAccounting();
        await db.Tasks.InsertOrUpdateAsync(
            () => new TesTask { Id = message.TaskId, Status = TesTaskStatus.Completed },
            x => new TesTask { Id = x.Id, Name = x.Name, Description = x.Description, Assigned = x.Assigned, Status = TesTaskStatus.Completed, Fee = x.Fee, Reward = x.Reward });
        await ApplyRewardTransaction(message.TaskId);
    }

    private async Task ApplyFeeTransaction(Guid userId, Guid taskId)
    {
        using var db = new DbAccounting();

        var task = await db.Tasks.FirstOrDefaultAsync(x => x.Id == taskId);
        // в реальной жизни я бы отложил это действие (или событие), и оно бы обработалось фоновым процессом позже
        while(task == null || task.Fee == null)
        {
            await Task.Delay(5 * 1000);
            task = await db.Tasks.FirstOrDefaultAsync(x => x.Id == taskId);
        }

        var reason = $"User assigned to task {taskId}: [{task.JiraId}] {task.Name}.\r\n{task.Description}.";
        var transaction = new Transaction { Id = Guid.NewGuid(), User = userId, Reason = reason, Debit = 0, Credit = task.Fee.Value, CreatedAt = DateTimeOffset.UtcNow };
        await db.InsertAsync(transaction);
        await _transactionProducer.ProduceAsync(DateTime.UtcNow.Ticks.ToString(), new StreamingEvent<Transaction>(transaction));
    }

    private async Task ApplyRewardTransaction(Guid taskId)
    {
        using var db = new DbAccounting();

        var task = await db.Tasks.FirstOrDefaultAsync(x => x.Id == taskId);
        // в реальной жизни я бы отложил это действие (или событие), и оно бы обработалось фоновым процессом позже
        while (task == null || task.Reward == null || task.Assigned == default)
        {
            await Task.Delay(5 * 1000);
            task = await db.Tasks.FirstOrDefaultAsync(x => x.Id == taskId);
        }

        var reason = $"User complete task {taskId}: [{task.JiraId}] {task.Name}.\r\n{task.Description}.";
        var transaction = new Transaction { Id = Guid.NewGuid(), User = task.Assigned, Reason = reason, Debit = task.Reward.Value, Credit = 0, CreatedAt = DateTimeOffset.UtcNow };
        await db.InsertAsync(transaction);
        await _transactionProducer.ProduceAsync(DateTime.UtcNow.Ticks.ToString(), new StreamingEvent<Transaction>(transaction));
    }
}

public class BillingCycleService : BackgroundService
{
    private readonly TimeSpan _billingCycleEndTime = new(23, 0, 0);
    private readonly TimeSpan _billingCycleStartTime = new(0, 0, 0);
    private bool _isBillingCycleClosed;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_isBillingCycleClosed && DateTime.Now.TimeOfDay > _billingCycleEndTime)
        {
            _isBillingCycleClosed = true;
            await CloseBillingCycle();
        }
        if (_isBillingCycleClosed && DateTime.Now.TimeOfDay < _billingCycleEndTime && DateTime.Now.TimeOfDay > _billingCycleStartTime)
        {
            _isBillingCycleClosed = false;
        }

        await Task.Delay(60 * 1000, stoppingToken);
    }

    private async Task CloseBillingCycle()
    {
        using var db = new DbAccounting();
        using var tr = await db.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var usersPayouts = new List<(Guid UserId, float PayoutAmount)>();
        try
        {
            var transactions = await db.Transactions.ToListAsync();
            var users = transactions.Select(x => x.User).Distinct();
            foreach(var user in users)
            {
                var userTransactions = transactions.Where(x => x.User == user);
                var balance = userTransactions.Sum(x => x.Debit - x.Credit);
                if (balance > 0)
                {
                    await Payout(db, user, balance);
                    usersPayouts.Add((user, balance));
                }
            }

            await tr.CommitAsync();
        }
        catch (Exception)
        {
            await tr.RollbackAsync();
        }

        foreach(var userPayout in usersPayouts)
        {
            NotifyUser(userPayout.UserId, userPayout.PayoutAmount);
        }
    }

    private async Task Payout(DbAccounting db, Guid user, float amount)
    {
        var transaction = new Transaction { Id = Guid.NewGuid(), User = user, Reason = $"Payout for user {user}", Debit = 0, Credit = amount, CreatedAt = DateTimeOffset.UtcNow };
        await db.InsertAsync(transaction);
    }

    private void NotifyUser(Guid user, float payoutAmount)
    {
        try
        {
            //тут должен быть код который отправляет на почту юзеру письмо с суммой выплаты
            //но я забыл про email
        }
        catch 
        { 
            // логгирование
        }
    }
}

public class DbAccounting : LinqToDB.Data.DataConnection
{
    public DbAccounting() : base(ProviderName.SqlServer2019, @"Server=ANTON-PC\SQLEXPRESS;Database=Accounting;Trusted_Connection=True;TrustServerCertificate=True;Enlist=False;", CreateMappingSchema()) { }

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
            .HasPrimaryKey(x => x.Id);
        builder.Entity<Transaction>()
            .HasTableName("Transaction")
            .HasPrimaryKey(x => x.Id);

        return mappingSchema;


    }

    public ITable<TesTask> Tasks => this.GetTable<TesTask>();
    public ITable<User> TaskUsers => this.GetTable<User>();
    public ITable<Transaction> Transactions => this.GetTable<Transaction>();
}