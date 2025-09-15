using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WebReport78.Models;

namespace WebReport78.Services
{
    // Interface cho xuất báo cáo Excel (có thể dùng chung cho InOut và Lpr nếu logic tương tự)
    public interface IReportService
    {
        Task<FileContentResult> ExportInOutReportAsync(string fromDate, string toDate, string note, string filterType, string locationId);
        // Có thể thêm phương thức khác nếu cần
    }
}