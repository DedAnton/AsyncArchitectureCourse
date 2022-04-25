using System.Text;
using System.Text.Json;

namespace Shared;

public static class Auth
{
    public static string BuildUserToken(User user)
    {
        var json = JsonSerializer.Serialize(user);
        var bytes = Encoding.UTF8.GetBytes(json);
        var token = Convert.ToBase64String(bytes);

        return token;
    }

    public static User? ReadUserToken(string token)
    {
        var bytes = Convert.FromBase64String(token);
        var json = Encoding.UTF8.GetString(bytes);
        var user = JsonSerializer.Deserialize<User>(json);

        return user;
    }
}