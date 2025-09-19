using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using WebReport78.Services;
using WebReport78.Interfaces;

namespace WebReport78.Controllers
{
    public class AuthController : Controller
    {
        private readonly ILogger<AuthController> _logger;
        private readonly IAuthService _authService;

        public AuthController(ILogger<AuthController> logger, IAuthService authService)
        {
            _logger = logger;
            _authService = authService;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Index(string email, string password)
        {
            try
            {
                if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
                {
                    ViewBag.ErrorMessage = "Vui lòng nhập đầy đủ thông tin.";
                    return View(); // Trả về lại view để overlay có thể bị ẩn
                }

                var (success, role) = await _authService.LoginAsync(email, password);
                if (success)
                {
                    _logger.LogInformation("Đăng nhập thành công cho email: {Email} với vai trò: {Role}", email, role);
                    // Lưu role vào session hoặc cookie (tùy cách bạn quản lý auth)
                    HttpContext.Session.SetString("Role", role);
                    HttpContext.Session.SetString("Email", email);
                    HttpContext.Session.SetString("Role", role);
                    return RedirectToAction("Index", "ProtectDuty");
                }
                else
                {
                    _logger.LogWarning("Đăng nhập thất bại cho email: {Email}", email);
                    ViewBag.ErrorMessage = "Email hoặc mật khẩu không đúng.";
                    return View();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đăng nhập cho email: {Email}", email);
                ViewBag.ErrorMessage = "Đã xảy ra lỗi khi đăng nhập.";
                return View();
            }
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index");
        }
    }
}