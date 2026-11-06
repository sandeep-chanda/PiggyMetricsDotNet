namespace PiggyMetrics.AuthService.Service.Security;

public sealed class UsernameNotFoundException : Exception
{
    public UsernameNotFoundException(string username)
        : base(username)
    {
    }
}
