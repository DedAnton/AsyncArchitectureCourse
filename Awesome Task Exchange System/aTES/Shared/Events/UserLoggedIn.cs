namespace Shared;

public class UserLoggedIn : Event
{
    public Guid UserId { get; }

    public UserLoggedIn(Guid userId)
        :base()
    {
        UserId = userId;
    }
}