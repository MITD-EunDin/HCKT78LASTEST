using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using WebReport78.Model2s;
using WebReport78.Models;

namespace WebReport78.Repositories
{
    public class GatewayMemberRepository : IGatewayMemberRepository
    {
        private readonly IDbContextFactory<H2XSmartContext> _contextFactory;

        public GatewayMemberRepository(IDbContextFactory<H2XSmartContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<TblGatewayMember> GetMemberByEmailAsync(string email)
        {
            using var context = _contextFactory.CreateDbContext();
            return await context.TblGatewayMembers
                .FirstOrDefaultAsync(m => m.IdMember == email);
        }
    }
}