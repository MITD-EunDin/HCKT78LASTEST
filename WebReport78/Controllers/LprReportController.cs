using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using WebReport78.Models;
using WebReport78.Services;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using WebReport78.Services;

namespace WebReport78.Controllers
{
    public class LprReportController : Controller
    {
        private readonly ILogger<LprReportController> _logger;
        private readonly XGuardContext _context;
        private readonly MongoDbService _mongoService;
        private readonly IWebHostEnvironment _env;
        private readonly CameraSettings _cameraSettings;
        public LprReportController(ILogger<LprReportController> logger, XGuardContext context, MongoDbService mongoservice, IWebHostEnvironment env)
        {
            _logger = logger;
            _context = context;
            _mongoService = mongoservice;
            _env = env;
            _cameraSettings = LoadCameraSettings();
        }

        //load cấu hình camera từ settingcamera.json
        private CameraSettings LoadCameraSettings()
        {
            var settingcamera = Path.Combine(_env.ContentRootPath, "settingcamera.json");
            var cameraSettingsJson = System.IO.File.ReadAllText(settingcamera);
            return JsonSerializer.Deserialize<CameraSettings>(cameraSettingsJson);
        }

        // Action báo cáo biển số
        public async Task<IActionResult> Index(string fromDate, string toDate, string filterType = "All", int page = 1, int pageSize = 100)
        {
            // Parse date range
            var (parsedFromDate, parsedToDate, fromTimestamp, toTimestamp) = ParseDateRange(fromDate, toDate);

            // Lấy camera settings
            var locationId = _cameraSettings.location_id;

            DateTime today = DateTime.Today;
            long todayFromTs = TimeStampHelper.ConvertToUnixTimestamp(today);
            long todayToTs = TimeStampHelper.ConvertToUnixTimestamp(today.AddHours(23).AddMinutes(59));

            // Truy vấn dữ liệu từ MongoDB
            var collection = _mongoService.GetCollection<eventLog>("EventLog");
            var filter = Builders<eventLog>.Filter.And(
               Builders<eventLog>.Filter.Gte(x => x.time_stamp, fromTimestamp),
                Builders<eventLog>.Filter.Lte(x => x.time_stamp, toTimestamp),
                Builders<eventLog>.Filter.Eq(x => x.locationId, locationId),
                Builders<eventLog>.Filter.Eq(x => x.typeEvent, 25) // 25 là sự kiện biển số
            );

            var totalRecords = await collection.CountDocumentsAsync(filter);
            var data = await collection.Find(filter)
                                      .SortBy(x => x.time_stamp)
                                      .Skip((page - 1) * pageSize)
                                      .Limit(pageSize)
                                      .ToListAsync();

            // Xử lý dữ liệu để hiển thị
            var viewModel = await ProcessLprEvents(data, locationId, fromTimestamp, toTimestamp);

            // Lọc theo filterType
            if (filterType == "EVENT_USER_FACE_LPR_MISMATCH")
            {
                viewModel = viewModel.Where(x => !string.IsNullOrEmpty(x.Warning)).ToList();
            }

            // Lưu vào ViewBag để hiển thị
            ViewBag.FromDate = parsedFromDate;
            ViewBag.ToDate = parsedToDate;
            ViewBag.Filter = filterType;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize);

            return View(viewModel);
        }

        // xử lý dữ liệu báo cáo biển số
        // (logic tìm những event_type = 25, lấy trước sự kiện đó 1s để lấy tên người cầm lái, từ đó cảnh báo biển số và mặt)
        private async Task<List<LprEventViewModel>> ProcessLprEvents(List<eventLog> data, string locationId, long fromTimestamp, long toTimestamp)
        {
            var sources = _context.Sources.Select(s => new { s.Guid, s.Name }).ToList();
            var vehicles = _context.Vehicles.Select(v => new { v.Lpn, v.Owner }).ToList();
            var camerasLprPair = _cameraSettings.camerasLprPair;


            // Lấy source_id của camera face và lpr từ camerasLprPair
            var faceCamera = camerasLprPair.FirstOrDefault(c => c.type == "face");
            var lprCamera = camerasLprPair.FirstOrDefault(c => c.type == "lpr");
            string faceSourceId = faceCamera?.source_id;
            string lprSourceId = lprCamera?.source_id;

            var viewModel = new List<LprEventViewModel>();

            // lấy lấy sự kiện trước đó (event_type = 1 hoặc -1)
            var collection = _mongoService.GetCollection<eventLog>("EventLog");
            var frFilter = Builders<eventLog>.Filter.And(
                Builders<eventLog>.Filter.Gte(x => x.time_stamp, fromTimestamp - 1), // lấy 1s trước
                Builders<eventLog>.Filter.Lte(x => x.time_stamp, toTimestamp),
                Builders<eventLog>.Filter.Eq(x => x.locationId, locationId),
                Builders<eventLog>.Filter.In(x => x.typeEvent, new[] { 1, -1 }), // những sk face
                Builders<eventLog>.Filter.Eq(x => x.sourceID, faceSourceId)
            );

            var frEvents = await collection.Find(frFilter).ToListAsync();

            foreach (var item in data)
            {
                // Chỉ xử lý sự kiện LPR từ camera type "lpr"
                if (lprSourceId == null || item.sourceID != lprSourceId)
                {
                    continue;
                }

                // Tìm sk face xảy ra trước event_type = 25 trong vòng 1s 
                var frEvent = frEvents
                    .Where(f => f.time_stamp <= item.time_stamp && item.time_stamp - f.time_stamp <= 1)
                    .OrderByDescending(f => f.time_stamp)
                    .FirstOrDefault();

                // so sánh idcamera trong log == idcamera trong xguard ?
                var source = sources.FirstOrDefault(s => s.Guid == item.sourceID);
                // so sánh biển số trong log == trong xguard ?
                var vehicle = vehicles.FirstOrDefault(v => v.Lpn == item.Name);

                // Lấy tên chủ sở hữu và người cầm lái
                var ownerName = vehicle?.Owner ?? "Unknown";
                var driverName = frEvent?.Name ?? "Unknown";
                var frCamera = frEvent != null ? (sources.FirstOrDefault(s => s.Guid == frEvent.sourceID)?.Name ?? frEvent.sourceID) : "Unkown";// lấy tên camera nhận mặt

                // Kiểm tra biển-mặt không khớp
                var warning = string.Equals(ownerName, driverName, StringComparison.OrdinalIgnoreCase) || driverName == "Unknown"
                    ? ""
                    : "Biển mặt không khớp";

                var model = new LprEventViewModel
                {
                    Timestamp = item.time_stamp, 
                    formatted_date = TimeStampHelper.ConvertTimestamp(item.time_stamp),
                    LicensePlate = item.Name ?? "Unknown",
                    Owner = GetUserNameFromGuid(item.Name),
                    CameraLpr = source?.Name ?? item.sourceID,
                    DirverName = driverName,
                    CameraFr = frEvent != null ? frCamera : "Unknown",
                    Warning = warning
                };
                viewModel.Add(model);
            }

            return viewModel;
        }

        private (DateTime parsedFromDate, DateTime parsedToDate, long fromTimestamp, long toTimestamp) ParseDateRange(string fromDateStr, string toDateStr)
        {
            var format = "dd-MM-yyyy HH:mm";
            var culture = CultureInfo.InvariantCulture;

            DateTime parsedFromDate;
            DateTime parsedToDate;

            if (!DateTime.TryParseExact(fromDateStr, format, culture, DateTimeStyles.None, out parsedFromDate))
            {
                parsedFromDate = DateTime.Today; // mặc định hôm nay
            }

            if (!DateTime.TryParseExact(toDateStr, format, culture, DateTimeStyles.None, out parsedToDate))
            {
                parsedToDate = DateTime.Today.AddHours(23).AddMinutes(59); // hết ngày hôm nay
            }

            long fromTimestamp = TimeStampHelper.ConvertToUnixTimestamp(parsedFromDate);
            long toTimestamp = TimeStampHelper.ConvertToUnixTimestamp(parsedToDate);

            return (parsedFromDate, parsedToDate, fromTimestamp, toTimestamp);
        }
        // xuất excel
        public async Task<IActionResult> ExReport(string fromDate, string toDate, string filterType, string note)
        {
            try
            {
                // Parse date range
                var (parsedFromDate, parsedToDate, fromTimestamp, toTimestamp) = ParseDateRange(fromDate, toDate);

                // Lấy camera settings
                var cameraSettings = LoadCameraSettings();
                var locationId = cameraSettings.location_id;

                // Truy vấn toàn bộ dữ liệu từ MongoDB
                var collection = _mongoService.GetCollection<eventLog>("EventLog");
                var filter = Builders<eventLog>.Filter.And(
                    Builders<eventLog>.Filter.Gte(x => x.time_stamp, fromTimestamp),
                    Builders<eventLog>.Filter.Lte(x => x.time_stamp, toTimestamp),
                    Builders<eventLog>.Filter.Eq(x => x.locationId, locationId),
                    Builders<eventLog>.Filter.Eq(x => x.typeEvent, 25) // 25 là sự kiện biển số
                );

                var data = await collection.Find(filter)
                                           .SortBy(x => x.time_stamp)
                                           .ToListAsync();

                // đổi dữ liệu thành LprEventViewModel
                var viewModel = await ProcessLprEvents(data, locationId, fromTimestamp, toTimestamp);

                // Lọc theo filterType
                if (filterType == "EVENT_USER_FACE_LPR_MISMATCH")
                {
                    viewModel = viewModel.Where(x => !string.IsNullOrEmpty(x.Warning)).ToList();
                }

                var folder = @"D:\excel";
                var templatePath = Path.Combine(folder, "_TemplateLicensePlate.xlsx");

                ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
                var fileName = $"LicensePlate_Report_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

                var stream = new MemoryStream();
                using (var package = new ExcelPackage(new FileInfo(templatePath)))
                {
                    var worksheet = package.Workbook.Worksheets.FirstOrDefault();
                    if (worksheet == null)
                        return StatusCode(500, "Không tìm thấy worksheet trong file template.");

                    var startDate = DateTime.ParseExact(fromDate, "dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture);
                    var endDate = DateTime.ParseExact(toDate, "dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture);

                    worksheet.Cells["C2"].Value = $"{startDate:dd-MM-yyyy HH:mm} - {endDate:dd-MM-yyyy HH:mm}";
                    worksheet.Cells["C3"].Value = $"{filterType}";
                    worksheet.Cells["C4"].Value = string.IsNullOrWhiteSpace(note) ? "Không có ghi chú" : note;

                    for (int i = 0; i < viewModel.Count; i++)
                    {
                        var row = i + 6;
                        worksheet.Cells[row, 1].Value = (i + 1).ToString();
                        worksheet.Cells[row, 2].Value = viewModel[i].formatted_date;
                        worksheet.Cells[row, 3].Value = viewModel[i].LicensePlate;
                        worksheet.Cells[row, 4].Value = viewModel[i].Owner;
                        worksheet.Cells[row, 5].Value = viewModel[i].CameraLpr;
                        worksheet.Cells[row, 6].Value = viewModel[i].DirverName;
                        worksheet.Cells[row, 7].Value = viewModel[i].CameraFr;
                    }

                    package.SaveAs(stream);
                }

                stream.Position = 0;
                return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi xuất file: {ex.Message}");
            }
        }

        // lấy tên người dùng từ LicensePlate
        private string GetUserNameFromGuid(string Name)
        {
            if (string.IsNullOrEmpty(Name)) return "Unknown";
            var vehicle = _context.Vehicles.FirstOrDefault(s => s.Lpn == Name);
            return vehicle?.Owner ?? "Unknown";
        }
    }
}