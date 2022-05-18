var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKafka(kafka => kafka
    .UseConsoleLog()
    .AddCluster(x => x
        .WithBrokers(new[] { "localhost:9092" })
        .AddProducer("accountProducer", "users")
    )
);
builder.Services.AddMyAuthorization();
builder.Services.AddMySwagger();

var app = builder.Build();
app.UseKafka(app.Lifetime);
app.UseMyAuthorization();
app.UseMySwagger();

app.MapPost("/login", [AllowAnonymous] async (string username, string password) =>
{
    using var db = new DbAuthorization();
    var user = db.Users.FirstOrDefault(x => x.Username == username && x.Password == password);
    if (user == null)
    {
        return Results.NotFound();
    }

    await ProduceEventAsync(new UserLoggedIn(user.Id));

    return Results.Ok(new { Token = TokenService.BuildToken(user) });
});

app.MapPost("/users", [Authorize(Roles = "Admin")] async (User newUser) =>
{
    using var db = new DbAuthorization();
    await db.InsertAsync(newUser);

    await ProduceEventAsync(new StreamingEvent<User>(newUser));

    return Results.Ok();
});

app.MapPut("/users/", [Authorize(Roles = "Admin")] async (User updatedUser) =>
{
    using var db = new DbAuthorization();
    await db.UpdateAsync(updatedUser);

    await ProduceEventAsync(new StreamingEvent<User>(updatedUser));

    return Results.Ok();
});

async Task ProduceEventAsync<T>(T @event) where T : Event
{
    var producers = app!.Services.GetRequiredService<IProducerAccessor>();
    await producers["accountProducer"].ProduceAsync(DateTime.UtcNow.Ticks.ToString(), @event);
}

app.Run($"http://localhost:5001");

public class DbAuthorization : LinqToDB.Data.DataConnection
{
    public DbAuthorization() : base(ProviderName.SqlServer2019, @"Server=ANTON-PC\SQLEXPRESS;Database=Authorization;Trusted_Connection=True;TrustServerCertificate=True;Enlist=False;", CreateMappingSchema()) { }

    private static MappingSchema CreateMappingSchema()
    {
        var mappingSchema = new MappingSchema();
        var builder = mappingSchema.GetFluentMappingBuilder();

        builder.Entity<User>()
            .HasTableName("User")
            .HasPrimaryKey(x => x.Id);

        return mappingSchema;
    }

    public ITable<User> Users => this.GetTable<User>();
}
