using LinqToDB.Mapping;

namespace Shared;

public class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string Role { get; set; } = "";
    public bool IsDeleted { get; set; } = false;
}