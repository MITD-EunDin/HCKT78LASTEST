using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using WebReport78.Model2s;
using WebReport78.Models;

namespace WebReport78.Repositories
{
    public class SsoUserRepository : ISsoUserRepository
    {
        private readonly IDbContextFactory<H2XSmartContext> _contextFactory;

        public SsoUserRepository(IDbContextFactory<H2XSmartContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<TblSsoUser> GetUserByEmailAsync(string email)
        {
            using var context = _contextFactory.CreateDbContext();
            return await context.TblSsoUsers
                .FirstOrDefaultAsync(u => u.Email == email);
        }
    }
}