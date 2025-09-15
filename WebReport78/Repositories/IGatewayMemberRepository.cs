using System.Threading.Tasks;
using WebReport78.Model2s;

namespace WebReport78.Repositories
{
    public interface IGatewayMemberRepository
    {
        Task<TblGatewayMember> GetMemberByEmailAsync(string email);
    }
}