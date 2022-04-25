using LinqToDB;
using Shared;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services
    .AddKafka("broker:9092");

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapPost("/authorize", (string username, string password) =>
{
    using var db = new DbAuthorization();
    var user = db.Users.FirstOrDefault(x => x.Username == username && x.Password == password);
    if (user == null)
    {
        return Results.NotFound($"Username {username} was not found");
    }

    return Results.Ok(new { Token = Auth.BuildUserToken(user) });
});

app.MapPost("/users", ([Microsoft.AspNetCore.Mvc.FromHeader(Name = "AuthToken")] string token, User newUser) =>
{
    using var db = new DbAuthorization();
    db.Users.Insert(() => newUser);

    return Results.Ok();
});

app.Run($"http://localhost:5001");

public class DbAuthorization : LinqToDB.Data.DataConnection
{
    public DbAuthorization() : base(@"Server=ANTON-PC\SQLEXPRESS;Database=Authorization;Trusted_Connection=True;Enlist=False;") { }

    public ITable<User> Users => GetTable<User>();
}