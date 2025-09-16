using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebReport78.Models;
using WebReport78.Repositories;
using WebReport78.Services;

namespace WebReport78.Controllers
{
    public class FirstInLastOutController : Controller
    {
        private readonly IInOutService _inOutService;
        private readonly IStaffRepository _staffRepo;

        public FirstInLastOutController(IInOutService inOutService, IStaffRepository staffRepo)
        {
            _inOutService = inOutService;
            _staffRepo = staffRepo;
        }

        public async Task<IActionResult> Index(string fromDate, string toDate, int? orgId, int? deptId, string[] employeeGuids)
        {
            var fromDateTime = string.IsNullOrEmpty(fromDate) ? DateTime.Today : DateTime.Parse(fromDate);
            var toDateTime = string.IsNullOrEmpty(toDate) ? DateTime.Today.AddHours(23).AddMinutes(59) : DateTime.Parse(toDate);

            var fromTs = TimeStampHelper.ConvertToUnixTimestamp(fromDateTime);
            var toTs = TimeStampHelper.ConvertToUnixTimestamp(toDateTime);

             var organizations = await _staffRepo.GetOrganizationsAsync();
            var departments = orgId.HasValue ? await _staffRepo.GetDepartmentsByOrgIdAsync(orgId.Value) : new List<Department>();
            var employees = await _staffRepo.GetStaffListAsync();

            if (orgId.HasValue)
                employees = employees.Where(e => e.IdOrg == orgId.Value).ToList();
            if (deptId.HasValue)
                employees = employees.Where(e => e.IdDept == deptId.Value).ToList();

            string locationId = "default_location"; // Thay bằng logic lấy locationId thực tế
            var firstInLastOut = await _inOutService.DoubleInOutAsync(
                fromTs,
                toTs,
                locationId,
                employeeGuids?.Length > 0 ? employeeGuids.First() : null
            );

            ViewBag.FromDate = fromDateTime;
            ViewBag.ToDate = toDateTime;
            ViewBag.Organizations = organizations;
            ViewBag.Departments = departments;
            ViewBag.Employees = employees;
            ViewBag.FirstInLastOut = firstInLastOut;

            return View(new List<eventLog>());
        }

        [HttpGet]
        public async Task<IActionResult> GetDepartments(int orgId)
        {
            var departments = await _staffRepo.GetDepartmentsByOrgIdAsync(orgId);
            return Json(departments);
        }

        [HttpGet]
        public async Task<IActionResult> GetEmployees(int? orgId, int? deptId)
        {
            var employees = await _staffRepo.GetStaffListAsync();
            if (orgId.HasValue)
                employees = employees.Where(e => e.IdOrg == orgId.Value).ToList();
            if (deptId.HasValue)
                employees = employees.Where(e => e.IdDept == deptId.Value).ToList();
            var employeeDtos = employees.Select(e => new { id = e.GuidStaff, name = e.Name, guidStaff = e.GuidStaff }).ToList();
            return Json(employeeDtos);
        }
    }
}