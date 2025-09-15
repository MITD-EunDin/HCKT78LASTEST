using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WebReport78.Models;
using WebReport78.Repositories;
using WebReport78.Services;

namespace WebReport78.Controllers
{
    public class InOutController : Controller
    {
        private readonly ILogger<InOutController> _logger;
        private readonly IInOutService _inOutService;
        private readonly IReportService _reportService;
        private readonly IJsonFileService _jsonService;
        private readonly IStaffRepository _staffRepo;

        public InOutController(ILogger<InOutController> logger, IInOutService inOutService, IReportService reportService, IJsonFileService jsonService, IStaffRepository staffRepo)
        {
            _logger = logger;
            _inOutService = inOutService;
            _reportService = reportService;
            _jsonService = jsonService;
            _staffRepo = staffRepo;
        }

        // Hiển thị trang báo cáo vào/ra
        public async Task<IActionResult> Index(string fromDate, string toDate, string filterType = "All", int page = 1, int pageSize = 100)
        {
            try
            {
                var (parsedFromDate, parsedToDate, fromTs, toTs) = _inOutService.ParseDateRange(fromDate, toDate);
                var locationId = _jsonService.GetLocationId();

                var (soldierTotal, soldierCurrent, guestCount, guestCurrent) = await _inOutService.GetSummaryAsync(fromTs, toTs, locationId, parsedFromDate);

                ViewData["SoldierTotal"] = soldierTotal;
                ViewData["SoldierCurrent"] = soldierCurrent;
                ViewData["GuestCount"] = guestCount;
                ViewData["GuestCurrent"] = guestCurrent;
                ViewBag.FromDate = parsedFromDate;
                ViewBag.ToDate = parsedToDate;
                ViewBag.Filter = filterType;
                ViewBag.Page = page;
                ViewBag.PageSize = pageSize;

                List<eventLog> data;
                if (filterType == "CurrentGuests")
                {
                    var guests = await _inOutService.GetCurrentGuestsAsync(parsedFromDate, parsedToDate);
                    data = guests.Select(g => new eventLog
                    {
                        Name = g.Name,
                        formatted_date = g.StartTime.HasValue ? TimeStampHelper.ConvertTimestamp(g.StartTime.Value) : "N/A",
                        idCard = g.DocumentNumber,
                        Gender = g.Gender == 1 ? "Nam" : "Nữ"
                    }).ToList();
                }
                else
                {
                    var validSources = (await _staffRepo.GetSourcesAsync()).Where(s => s.AcCheckType == 1 || s.AcCheckType == 2).Select(s => s.Guid).ToList();
                    data = await _inOutService.GetFilteredDataAsync(filterType, fromTs, toTs, locationId, parsedFromDate, parsedToDate, validSources);
                    data = data.Where(x => !string.IsNullOrEmpty(x.Name) && x.Name != "Unknown").ToList();
                }
                // Phân trang server-side cho TẤT CẢ filter
                var totalItems = data.Count;
                ViewBag.TotalPages = (int)Math.Ceiling((double)totalItems / pageSize);
                data = data.Skip((page - 1) * pageSize).Take(pageSize).ToList();
                //ViewBag.TotalPages = (int)Math.Ceiling((double)data.Count / pageSize);
                return View(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi trong InOut Index");
                return StatusCode(500, "Lỗi hệ thống nội bộ");
            }
        }

        // Kiểm tra mã định danh
        [HttpPost]
        public async Task<IActionResult> CheckIdCard(string idCard)
        {
            try
            {
                _logger.LogInformation("Kiểm tra idCard: {IdCard}", idCard);
                var staff = await _inOutService.CheckIdCardAsync(idCard);
                if (staff == null)
                    return Json(new { success = false, message = "Không tìm thấy quân nhân với mã định danh này." });

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        UserGuid_cur = staff.GuidStaff,
                        Name_cur = staff.Name,
                        IdCard_cur = staff.DocumentNumber,
                        Gender_cur = staff.Gender == 1 ? "Nam" : "Nữ",
                        PhoneNumber_cur = staff.Phone ?? ""
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi kiểm tra idCard: {IdCard}", idCard);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // Thêm quân nhân thủ công
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCurrentSoldier(string userGuid, string name, string idCard, string gender, int typePerson, string phone)
        {
            try
            {
                await _inOutService.AddCurrentSoldierAsync(userGuid, name, idCard, gender, phone);
                TempData["Success"] = "Check In thành công.";
                return Json(new { success = true, message = "Đã thêm quân nhân." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thêm quân nhân");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Xóa quân nhân thủ công
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveCurrentSoldier(string userGuid)
        {
            try
            {
                await _inOutService.RemoveCurrentSoldierAsync(userGuid);
                TempData["Success"] = "Check out thành công.";
                return Json(new { success = true, message = "Đã xóa quân nhân." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa quân nhân: {UserGuid}", userGuid);
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Khởi tạo danh sách quân nhân
        [HttpPost]
        public async Task<IActionResult> InitializeCurrentSoldiers()
        {
            try
            {
                await _inOutService.InitializeCurrentSoldiersAsync();
                return Ok("Đã khởi tạo danh sách quân số.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi khởi tạo danh sách quân số");
                return StatusCode(500, "Có lỗi xảy ra khi khởi tạo danh sách quân số.");
            }
        }

        // Xuất báo cáo Excel
        [HttpPost]
        public async Task<IActionResult> ExReport(string fromDate, string toDate, string note, string filterType = "All")
        {
            try
            {
                var locationId = _jsonService.GetLocationId();
                return await _reportService.ExportInOutReportAsync(fromDate, toDate, note, filterType, locationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xuất báo cáo InOut");
                return StatusCode(500, $"Lỗi khi xuất file: {ex.Message}");
            }
        }
    }
}