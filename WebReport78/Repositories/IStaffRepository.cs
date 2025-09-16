using System.Collections.Generic;
using System.Threading.Tasks;
using WebReport78.Models;

namespace WebReport78.Repositories
{
    // Interface cho truy cập dữ liệu từ SQL Server (Staff, Vehicles, Sources)
    public interface IStaffRepository
    {
        // Đếm tổng số quân nhân (IdTypePerson = 0 hoặc 2)
        Task<int> GetSoldierTotalAsync();
        // Lấy danh sách khách trong khoảng thời gian
        Task<List<Staff>> GetGuestsAsync(long fromTs, long toTs);
        // Đếm số khách hiện tại (chưa rời đi)
        Task<int> GetGuestCurrentTodayAsync(List<Staff> guests, long toTs);
        // Lấy danh sách quân nhân (cacheable)
        Task<List<Staff>> GetStaffListAsync();
        // Lấy danh sách xe (cacheable)
        Task<List<Vehicle>> GetVehiclesAsync();
        // Lấy danh sách nguồn (camera, cacheable)
        //Task<List<dynamic>> GetSourcesAsync();
        Task<List<Source>> GetSourcesAsync();

        // Tìm nhân viên theo DocumentNumber
        Task<Staff> GetStaffByDocumentNumberAsync(string idCard);

        //tổng số người có trong db cả loại 0 1 2 3 4
        //Task<List<Staff>> GetTotalEmployeeAsync();


        // Thêm hai phương thức mới
        Task<List<Organization>> GetOrganizationsAsync();
        Task<List<Department>> GetDepartmentsByOrgIdAsync(int orgId);
        Task<List<Staff>> GetStaffAsync(int IdOrg, int IdDept);
    }
}