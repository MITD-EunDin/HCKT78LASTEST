using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WebReport78.Interfaces;
using WebReport78.Models;
using WebReport78.Repositories;

namespace WebReport78.Controllers
{
    public class LprReportController : Controller
    {
        private readonly ILogger<LprReportController> _logger;
        private readonly ILprService _lprService;
        private readonly IInOutService _inOutService;
        private readonly IEventLogRepository _eventLogRepo;
        private readonly IJsonFileService _jsonService;

        public LprReportController(
            ILogger<LprReportController> logger,
            ILprService lprService,
            IInOutService inOutService,
            IEventLogRepository eventLogRepo,
            IJsonFileService jsonService)
        {
            _logger = logger;
            _lprService = lprService;
            _inOutService = inOutService;
            _eventLogRepo = eventLogRepo;
            _jsonService = jsonService;
        }

        public async Task<IActionResult> Index(string fromDate, string toDate, int page = 1, int pageSize = 100)
        {
            try
            {
                var (parsedFromDate, parsedToDate, fromTs, toTs) = _inOutService.ParseDateRange(fromDate, toDate);
                var locationId = _jsonService.GetLocationId();

                var data = await _lprService.GetLprEventLogsAsync(fromTs, toTs, locationId, page, pageSize);
                var totalRecords = await _eventLogRepo.GetTotalRecordsAsync(fromTs, toTs, locationId, 101);
                var viewModel = await _lprService.ProcessLprEventsAsync(data);

                ViewBag.FromDate = parsedFromDate;
                ViewBag.ToDate = parsedToDate;
                ViewBag.Page = page;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize);

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in LprReport Index");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExReport(string fromDate, string toDate, string note)
        {
            try
            {
                var locationId = _jsonService.GetLocationId();
                return await _lprService.ExportExcelAsync(fromDate, toDate, note, locationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting Lpr report");
                return StatusCode(500, $"Error exporting file: {ex.Message}");
            }
        }
    }
}