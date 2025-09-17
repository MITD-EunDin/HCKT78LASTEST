using System;
using System.Collections.Generic;
using System.Security.Cryptography;
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
        public async Task<IActionResult> Index(string fromDate, string toDate, string filterType = "All", int page = 1, int pageSize = 100, int orgId = 0, int deptId = 0, string employeeGuids = "")
        {
            try
            {
                var (parsedFromDate, parsedToDate, fromTs, toTs) = _inOutService.ParseDateRange(fromDate, toDate);
                var locationId = _jsonService.GetLocationId();

                var (soldierTotal, soldierCurrent, guestCount, guestCurrent) = await _inOutService.GetSummaryAsync(fromTs, toTs, locationId, parsedFromDate);

                // Dropdown dữ liệu
                var orgs = await _staffRepo.GetOrganizationsAsync();
                var depts = orgId > 0 ? await _staffRepo.GetDepartmentsByOrgIdAsync(orgId) : new List<Department>();
                var employees = await _staffRepo.GetStaffAsync(orgId, deptId);

                // Xử lý employeeGuids
                var selectedEmployeeGuids = string.IsNullOrEmpty(employeeGuids) ? new List<string>() : employeeGuids.Split(',').ToList();
                var selectedEmployees = employees.Where(e => selectedEmployeeGuids.Contains(e.GuidStaff)).Select(e => new Staff { GuidStaff = e.GuidStaff, Name = e.Name }).ToList();

                // ViewBag cho dropdown và các thông tin khác
                ViewBag.Organizations = orgs;
                ViewBag.Departments = depts;
                ViewBag.Employees = employees;
                ViewBag.SelectedOrgId = orgId;
                ViewBag.SelectedDeptId = deptId;
                ViewBag.SelectedEmployeeGuids = selectedEmployeeGuids;
                ViewBag.SelectedEmployees = selectedEmployees;

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
                else if (filterType == "FILO")
                {
                    var filoData = await _inOutService.DoubleInOutAsync(fromTs, toTs, locationId, selectedEmployeeGuids.Any() ? string.Join(",", selectedEmployeeGuids) : null);
                    data = filoData.Select(kvp => new eventLog
                    {
                        userGuid = kvp.Key,
                        Name = employees.FirstOrDefault(e => e.GuidStaff == kvp.Key)?.Name ?? "N/A",
                        formatted_date = kvp.Value.FirstIn?.ToString("dd-MM-yyyy HH:mm") ?? "N/A",
                        type_eventIO = kvp.Value.LastOut?.ToString("dd-MM-yyyy HH:mm") ?? "N/A",
                        cameraName = kvp.Value.CameraName
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
        // Lấy Employees theo Org + Dept
        [HttpGet]
        public async Task<IActionResult> GetEmployees(int? orgId, int? deptId)
        {
            var employees = await _staffRepo.GetStaffAsync(orgId, deptId);
            return Json(employees.Select(e => new { guidStaff = e.GuidStaff, name = e.Name }));
        }

        // Lấy Departments theo Org
        [HttpGet]
        public async Task<IActionResult> GetDepartments(int orgId)
        {
            var depts = await _staffRepo.GetDepartmentsByOrgIdAsync(orgId);
            return Json(depts.Select(d => new { idDept = d.IdDept, name = d.Name }));
        }

        // Gọi DoubleInOutAsync
        [HttpGet]
        public async Task<IActionResult> GetInOutTimes(string fromDate, string toDate, string employeeGuids)
        {
            try
            {
                var guids = string.IsNullOrEmpty(employeeGuids) ? new List<string>() : employeeGuids.Split(',').ToList();
                var (parsedFromDate, parsedToDate, fromTs, toTs) = _inOutService.ParseDateRange(fromDate, toDate);
                var locationId = _jsonService.GetLocationId();

                var result = new Dictionary<string, (DateTime? FirstIn, DateTime? LastOut, string CameraName)>();
                foreach (var guid in guids)
                {
                    var partialResult = await _inOutService.DoubleInOutAsync(fromTs, toTs, locationId, guid);
                    foreach (var kvp in partialResult)
                    {
                        result[kvp.Key] = kvp.Value;
                    }
                }

                if (!result.Any())
                {
                    return Json(new { success = false, message = "Không tìm thấy dữ liệu check-in/check-out." });
                }

                var response = result.Select(r => new
                {
                    UserGuid = r.Key,
                    FirstIn = r.Value.FirstIn?.ToString("dd-MM-yyyy HH:mm"),
                    LastOut = r.Value.LastOut?.ToString("dd-MM-yyyy HH:mm"),
                    CameraName = r.Value.CameraName
                });

                return Json(new { success = true, data = response });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy thời gian check-in/check-out");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống nội bộ" });
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