using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WebReport78.Models;
using WebReport78.Repositories;

namespace WebReport78.Services
{
    public interface IFirstInCheckoutService
    {
        Task<List<Organization>> GetOrganizationsAsync();
        Task<List<Department>> GetDepartmentsByOrgAsync(int orgId);
        Task<List<Staff>> GetEmployeesByDeptOrOrgAsync(int? deptId, int? orgId);
        Task<Dictionary<string, (DateTime? FirstIn, DateTime? LastOut, string CameraName)>> GetFirstInLastOutAsync(List<string> employeeGuids, List<eventLog> eventLogs);
    }

    public class FirstInCheckoutService : IFirstInCheckoutService
    {
        private readonly IStaffRepository _staffRepo;
        private readonly ILogger<FirstInCheckoutService> _logger;
        private readonly IDbContextFactory<XGuardContext> _contextFactory;

        public FirstInCheckoutService(
            IStaffRepository staffRepo,
            ILogger<FirstInCheckoutService> logger,
            IDbContextFactory<XGuardContext> contextFactory)
        {
            _staffRepo = staffRepo;
            _logger = logger;
            _contextFactory = contextFactory;
        }

        public async Task<List<Organization>> GetOrganizationsAsync()
        {
            using var context = _contextFactory.CreateDbContext();
            return await context.Organizations
                .OrderBy(o => o.OrderNo)
                .ToListAsync();
        }

        public async Task<List<Department>> GetDepartmentsByOrgAsync(int orgId)
        {
            using var context = _contextFactory.CreateDbContext();
            return await context.Departments
                .Where(d => d.IdOrg == orgId)
                .OrderBy(d => d.OrderNo)
                .ToListAsync();
        }

        public async Task<List<Staff>> GetEmployeesByDeptOrOrgAsync(int? deptId, int? orgId)
        {
            using var context = _contextFactory.CreateDbContext();
            var query = context.Staff.AsQueryable();

            if (deptId.HasValue)
            {
                query = query.Where(s => s.IdDept == deptId.Value);
            }
            else if (orgId.HasValue)
            {
                query = query.Where(s => s.IdOrg == orgId.Value);
            }

            query = query.Where(s => s.IdTypePerson.HasValue && (s.IdTypePerson.Value == 0 || s.IdTypePerson.Value == 2));

            return await query
                .OrderBy(s => s.OrderNo)
                .ToListAsync();
        }

        public async Task<Dictionary<string, (DateTime? FirstIn, DateTime? LastOut, string CameraName)>> GetFirstInLastOutAsync(List<string> employeeGuids, List<eventLog> eventLogs)
        {
            var sources = await _staffRepo.GetSourcesAsync();
            var validSourceGuids = sources.Select(s => s.Guid).ToList();
            var filteredLogs = eventLogs
                .Where(e => (e.typeEvent == 1 || e.typeEvent == 25) && validSourceGuids.Contains(e.sourceID))
                .ToList();

            // Gán camera name và type_eventIO nếu chưa được gán bởi InOutService
            foreach (var log in filteredLogs)
            {
                if (string.IsNullOrEmpty(log.cameraName) || string.IsNullOrEmpty(log.type_eventIO))
                {
                    var source = sources.FirstOrDefault(s => s.Guid == log.sourceID);
                    log.cameraName = source?.Name ?? log.sourceID;
                    log.type_eventIO = source?.AcCheckType == 2 ? "Check-In" : "Check-Out";
                }
            }

            var filoDict = new Dictionary<string, (DateTime? FirstIn, DateTime? LastOut, string CameraName)>();

            // Nhóm event logs theo employee Guid
            var groupedLogs = filteredLogs
                .Where(e => employeeGuids.Contains(e.userGuid))
                .GroupBy(e => e.userGuid);

            foreach (var group in groupedLogs)
            {
                var key = group.Key;
                var checkIns = group.Where(e => e.type_eventIO == "Check-In").OrderBy(e => e.time_stamp).ToList();
                var checkOuts = group.Where(e => e.type_eventIO == "Check-Out").OrderByDescending(e => e.time_stamp).ToList();

                DateTime? firstIn = checkIns.Any() ? TimeStampHelper.ConvertTimestampToDateTime(checkIns.First().time_stamp) : null;
                DateTime? lastOut = checkOuts.Any() ? TimeStampHelper.ConvertTimestampToDateTime(checkOuts.First().time_stamp) : null;
                string cameraName = checkIns.Any() ? checkIns.First().cameraName : (checkOuts.Any() ? checkOuts.First().cameraName : "N/A");

                filoDict[key] = (firstIn, lastOut, cameraName);
            }

            // Đảm bảo tất cả employeeGuids có trong dictionary
            foreach (var guid in employeeGuids)
            {
                if (!filoDict.ContainsKey(guid))
                {
                    filoDict[guid] = (null, null, "N/A");
                }
            }

            return filoDict;
        }
    }
}