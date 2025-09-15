using System.Collections.Generic;
using System.Threading.Tasks;
using WebReport78.Models;

namespace WebReport78.Repositories
{
    // Interface cho truy cập dữ liệu từ MongoDB (EventLog)
    public interface IEventLogRepository
    {
        // Đếm tổng số bản ghi theo khoảng thời gian, locationId, và typeEvent (nếu có)
        Task<long> GetTotalRecordsAsync(long fromTs, long toTs, string locationId, int? typeEvent = null);
        // Lấy danh sách sự kiện với phân trang
        Task<List<eventLog>> GetEventLogsAsync(long fromTs, long toTs, string locationId, int page, int pageSize, int? typeEvent = null);
    }
}