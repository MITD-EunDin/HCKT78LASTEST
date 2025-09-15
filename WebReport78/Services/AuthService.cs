using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using WebReport78.Model2s;
using WebReport78.Repositories;

namespace WebReport78.Services
{
    public class AuthService : IAuthService
    {
        private readonly ISsoUserRepository _ssoUserRepository;
        private readonly IGatewayMemberRepository _gatewayMemberRepository;
        private readonly ILogger<AuthService> _logger;


        public AuthService(ISsoUserRepository ssoUserRepository, IGatewayMemberRepository gatewayMemberRepository, ILogger<AuthService> logger)
        {
            _ssoUserRepository = ssoUserRepository;
            _gatewayMemberRepository = gatewayMemberRepository;
            _logger = logger;
        }

        private string HashPassword(string password, string salt)
        {
            //using (var sha256 = SHA256.Create())
            //{
            //    string saltedPassword = password + salt;
            //    byte[] bytes = Encoding.UTF8.GetBytes(saltedPassword);
            //    byte[] hash = sha256.ComputeHash(bytes);
            //    return BitConverter.ToString(hash).Replace("-", "").ToLower();
            //}
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(salt))
            {
                _logger.LogWarning("HashPassword: Password or salt is null or empty.");
                return null;
            }

            using (var sha256 = SHA256Managed.Create())
            {
                string Password = password.Trim();
                byte[] bytes = Encoding.UTF8.GetBytes(Password);
                _logger.LogInformation("byte = {bytes}", bytes);
                _logger.LogInformation("HashPassword: Salted password (before hash) = {Password}", Password);

                // hash lan 1
                byte[] hash = sha256.ComputeHash(bytes);
                string hashResult1 = BitConverter.ToString(hash).Replace("-", "").ToLower();
                _logger.LogInformation("hash lan 1 = {hashResult1}", hashResult1);
                //hash lan 2
                string saltPassword = (salt + hashResult1).Trim();
                byte[] byte2 = Encoding.UTF8.GetBytes(saltPassword);
                _logger.LogInformation("truoc khi hash lan 2 = {saltPassword}", saltPassword);
                byte[] hash2 = sha256.ComputeHash(byte2);
                string hashResult2 = BitConverter.ToString(hash2).Replace("-", "").ToLower();
                _logger.LogInformation("HashPassword: Hashed result = {HashResult2}", hashResult2);

                return hashResult2;
            }
        }

        public async Task<(bool Success, string Role)> LoginAsync(string email, string password)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
                return (false, null);

            var user = await _ssoUserRepository.GetUserByEmailAsync(email);
            if (user == null || user.Status != 1) // Giả sử Status = 1 là active
                return (false, null);

            string hashedPassword = HashPassword(password, user.Salt);
            if (user.Password != hashedPassword)
                return (false, null);

            var member = await _gatewayMemberRepository.GetMemberByEmailAsync(email);
            if (member == null)
                return (false, null);

            string role = member.IdRole; // "Super Admin", "Admin", "Member", "Temporary Admin", "Temporary Member"
            _logger.LogInformation("LoginAsync: Role retrieved = {Role}", role);
            return (true, role);

        }
    }
}