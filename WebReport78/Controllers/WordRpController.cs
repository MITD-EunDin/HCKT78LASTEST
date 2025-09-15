using Microsoft.AspNetCore.Mvc;
using Xceed.Words.NET;
using WebReport78.Models;
using WebReport78.Repositories;
using WebReport78.Services;
namespace WebReport78.Controllers
{
    public class WordRpController : Controller
    {
        private readonly IInOutService _inOutService;
        private readonly IJsonFileService _jsonService;
                private readonly IWebHostEnvironment _env;

        public WordRpController(IInOutService inOutService, IJsonFileService jsonService, IWebHostEnvironment env)
        {
            _inOutService = inOutService;
            _jsonService = jsonService;
            _env = env;
        }

        public IActionResult Index()
        {
            // Lấy dữ liệu mặc định để hiển thị trên view
            var (soldierTotal, soldierCurrent, _, _) = _inOutService.GetSummaryAsync(
                TimeStampHelper.ConvertToUnixTimestamp(DateTime.Today),
                TimeStampHelper.ConvertToUnixTimestamp(DateTime.Now),
                _jsonService.GetLocationId(),
                DateTime.Today
            ).Result;

            ViewData["SoldierTotal"] = soldierTotal;
            ViewData["SoldierCurrent"] = soldierCurrent;

            return View();
        }

        [HttpPost]
        public IActionResult ExportToWord(string fromDate, string toDate, string huhu)
        {

            try
            {
                // Parse khoảng thời gian
                var (parsedFromDate, parsedToDate, fromTs, toTs) = _inOutService.ParseDateRange(fromDate, toDate);
                var locationId = _jsonService.GetLocationId();

                // Lấy thông tin quân số
                var (soldierTotal, soldierCurrent, _, _) = _inOutService.GetSummaryAsync(fromTs, toTs, locationId, parsedFromDate).Result;

                // Định dạng thời gian cho {{tu}} và {{den}}
                string tuTime = parsedFromDate.ToString("dd/MM/yyyy HH:mm");  // Ví dụ: 08/09/2025 00:00
                string denTime = parsedToDate.ToString("dd/MM/yyyy HH:mm");

                string note = huhu;

                var folder = Path.Combine(_env.WebRootPath, "ReportTemplate");
                var templatePath = Path.Combine(folder, "TEST_BÁO_CÁO.docx");

                //string templatePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "templates", "Template.docx");
                var fileName = $"Bao_cao_word{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.docx";
                string outputPath = Path.Combine(Path.GetTempPath(), fileName);

                using (var doc = DocX.Load(templatePath))
                {
                    doc.ReplaceText("{{tu}}", tuTime);
                    doc.ReplaceText("{{den}}", denTime);

                    doc.ReplaceText("{{hehe}}", soldierCurrent.ToString() ?? "");
                    doc.ReplaceText("{{hihi}}", soldierTotal.ToString() ?? "");
                    doc.ReplaceText("{{huhu}}", note ?? "");


                    doc.SaveAs(outputPath);
                }

                var fileBytes = System.IO.File.ReadAllBytes(outputPath);
                return File(fileBytes,
                            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                            fileName);
            }
            catch (Exception ex)
            {
                // Log lỗi nếu cần
                return StatusCode(500, $"Lỗi khi xuất file Word: {ex.Message}");
            }
        }
    }
}

