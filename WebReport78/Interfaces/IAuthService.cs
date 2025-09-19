using System.Threading.Tasks;

namespace WebReport78.Interfaces
{
    public interface IAuthService
    {
        Task<(bool Success, string Role)> LoginAsync(string email, string password);
    }
}