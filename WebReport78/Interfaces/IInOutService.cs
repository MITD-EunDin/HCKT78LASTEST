using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WebReport78.Models;

namespace WebReport78.Interfaces
{
    // Interface cho logic chung của InOut và Lpr
    public interface IInOutService
    {
        // Parse khoảng thời gian từ string sang DateTime và Unix timestamp
        (DateTime fromDate, DateTime toDate, long fromTs, long toTs) ParseDateRange(string fromDateStr, string toDateStr);
        // Tính tổng số quân nhân hiện tại
        Task<int> CalculateCurrentSoldiersAsync(long fromTs, long toTs, string locationId);
        // Cập nhật danh sách quân nhân hiện tại từ sự kiện
        Task UpdateCurrentSoldiersFromEventsAsync(long fromTs, long toTs, string locationId);
        // Lấy danh sách quân nhân hiện tại
        Task<List<CurrentSoldier>> GetCurrentSoldiersAsync(long fromTs, long toTs, string locationId);
        // Lấy danh sách khách hiện tạihttps://grok.com/?voice=true
        Task<List<Staff>> GetCurrentGuestsAsync(DateTime fromDate, DateTime toDate);
        // Tìm nhân viên theo userGuid hoặc biển số (tức sự kiên 1 và 25 để xem lấy gì để so sánh)
        Task<Staff> GetStaffFromUserGuidAsync(string key);
        // Xử lý dữ liệu sự kiện cho InOut
        Task ProcessEventLogAsync(List<eventLog> data, DateTime fromDate, DateTime toDate);
        // Tính thời gian ra ngoài
        void CalculateOutTime(List<eventLog> data);
        // Lấy thông tin tóm tắt (quân số, khách)
        Task<(int soldierTotal, int soldierCurrent, int guestCount, int guestCurrent)> GetSummaryAsync(long fromTs, long toTs, string locationId, DateTime fromDate);
        // Thêm quân nhân thủ công
        Task AddCurrentSoldierAsync(string userGuid, string name, string idCard, string gender, string phone);
        // Xóa quân nhân thủ công
        Task RemoveCurrentSoldierAsync(string userGuid);
        // Khởi tạo danh sách quân nhân
        Task InitializeCurrentSoldiersAsync();
        // Kiểm tra mã định danh
        Task<Staff> CheckIdCardAsync(string idCard);
        // Lấy dữ liệu đã lọc theo type
        Task<List<eventLog>> GetFilteredDataAsync(string filterType, long fromTs, long toTs, string locationId, DateTime fromDate, DateTime toDate, List<string> validSources);

        // Lấy cặp check-in đầu tiên và check-out cuối cùng theo GuidStaff
        Task<Dictionary<string, (DateTime? FirstIn, DateTime? LastOut, string CameraNameIn, string CameraNameOut)>> GetFirstInLastOutAsync(long fromTs, long toTs, string locationId, string employeeGuid);
    }
}