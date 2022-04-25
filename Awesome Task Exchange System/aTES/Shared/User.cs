using LinqToDB.Mapping;

namespace Shared;

[Table(Name = "User")]
public class User
{
    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();
    [Column, NotNull]
    public string Username { get; set; } = "";
    [Column, NotNull]
    public string Password { get; set; } = "";
    [Column, NotNull]
    public string Role { get; set; } = "";
}