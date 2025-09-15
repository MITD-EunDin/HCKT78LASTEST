using System.Threading.Tasks;
using WebReport78.Model2s;

namespace WebReport78.Repositories
{
    public interface ISsoUserRepository
    {
        Task<TblSsoUser> GetUserByEmailAsync(string email);
    }
}