using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using WebReport78.Models;

namespace WebReport78.Services
{
    public interface ILprService
    {
        Task<List<eventLog>> GetLprEventLogsAsync(long fromTs, long toTs, string locationId, int page, int pageSize);
        Task<List<LprEventViewModel>> ProcessLprEventsAsync(List<eventLog> data);
        Task<FileContentResult> ExportExcelAsync(string fromDate, string toDate, string note, string locationId);
    }
}