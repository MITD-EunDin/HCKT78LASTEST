using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using WebReport78.Model2s;
using WebReport78.Repositories;
using WebReport78.Interfaces;

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

            try
            {
                _logger.LogInformation("HashPassword: Bắt đầu hash với Password = {Password}, Salt = {Salt}", password.Trim(), salt);

                //using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(salt)))
                //{
                //    byte[] passwordBytes = Encoding.UTF8.GetBytes(password);

                //    byte[] hash = hmac.ComputeHash(passwordBytes);
                //    _logger.LogInformation("Byte ecnoding password: {passwordBytes}");
                //    _logger.LogInformation("byte cái password ở trên sau khi hash là {hash}");

                //    string hashResult = BitConverter.ToString(hash).Replace("-", "").ToLower();

                //    _logger.LogInformation("HashPassword: Password = {Password}, Salt = {Salt}, Hashed result = {HashResult}",
                //        password.Trim(), salt, hashResult);
                //    return hashResult;
                //}
                // salt = key
                //using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(salt)))
                //{
                //    // password = message
                //    byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
                //    byte[] hash = hmac.ComputeHash(passwordBytes);
                //    string rs = BitConverter.ToString(hash).Replace("-", "").ToLower();

                //    _logger.LogInformation("HashPassword: Password = {Password}, Salt = {Salt}, Hashed result = {HashResult}", password.Trim(), salt, rs);
                //    // Chuyển sang hex lowercase
                //    //return BitConverter.ToString(hash).Replace("-", "").ToLower();
                //    return rs;
                //}
                string trimmedPassword = password;
                string trimSalt = salt;
                byte[] keyBytes = Encoding.UTF8.GetBytes(trimSalt);     // Salt đóng vai trò như key
                byte[] messageBytes = Encoding.UTF8.GetBytes(trimmedPassword); // Password là message

                _logger.LogInformation("HashPassword: Chuỗi password sau khi trim = {TrimmedPassword}", trimmedPassword);
                _logger.LogInformation("HashPassword: Salt (key) = {Salt}", trimSalt);
                _logger.LogInformation("HashPassword: Byte[] của key = {KeyBytes}", BitConverter.ToString(keyBytes));
                _logger.LogInformation("HashPassword: Byte[] của password = {MessageBytes}", BitConverter.ToString(messageBytes));


                using (var hmac = new HMACSHA256(keyBytes))
                {
                    byte[] hashBytes = hmac.ComputeHash(messageBytes);
                    string hashHex = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

                    _logger.LogInformation("HashPassword: Đã hash xong. Password = {Password}, Salt = {Salt}, Hash = {HashResult}",
                        trimmedPassword, salt, hashHex);
                    _logger.LogInformation("HashPassword: Byte[] sau khi hash = {HashBytes}", BitConverter.ToString(hashBytes));
                    _logger.LogInformation("HashPassword: Chuỗi hex sau khi hash = {HashHex}", hashHex);

                    return hashHex;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("HasPassWord: Error while hasing password");
                return null;
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