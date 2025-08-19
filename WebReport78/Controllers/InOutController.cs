using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using WebReport78.Models;
using OfficeOpenXml;
using System.Text.Json;
using WebReport78.Services;
using MongoDB.Driver;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using System.IO;
using WebReport78.Services;

namespace WebReport78.Controllers
{
    public class InOutController : Controller
    {
        private readonly ILogger<InOutController> _logger;
        private readonly XGuardContext _context;
        private readonly MongoDbService _mongoService;
        private readonly IWebHostEnvironment _env;
        public InOutController(ILogger<InOutController> logger, XGuardContext context, MongoDbService mongoservice, IWebHostEnvironment env)
        {
            _logger = logger;
            _context = context;
            _mongoService = mongoservice;
            _env = env;
        }

        // hiển thị ra html
        public async Task<IActionResult> Index(string fromDate, string toDate, string filterType = "All", int page = 1, int pageSize = 100)
        {

            // Parse date range
            var (parsedFromDate, parsedToDate, fromTimestamp, toTimestamp) = ParseDateRange(fromDate, toDate);

            // tải config json để biết loại vào ra và đi sớm về muộn
            var cameraSettings = LoadCameraSettings();
            var checkInCamera = cameraSettings.cameras.FirstOrDefault(c => c.type == "in")?.source_id;
            var checkOutCamera = cameraSettings.cameras.FirstOrDefault(c => c.type == "out")?.source_id;
            var locationId = cameraSettings.location_id;

            // tính quân số, số khách trong ngày
            DateTime today = DateTime.Today;
            int soldierTotal = await _context.Staff.CountAsync(s => s.IdTypePerson.HasValue && (s.IdTypePerson.Value == 0 || s.IdTypePerson.Value == 2));
            int guestCount = await _context.Staff.CountAsync(s => s.IdTypePerson.HasValue && s.IdTypePerson.Value == 3 && s.DateCreated.HasValue && s.DateCreated.Value.Date == today);
            long todayFromTs = TimeStampHelper.ConvertToUnixTimestamp(today);
            long todayToTs = TimeStampHelper.ConvertToUnixTimestamp(today.AddHours(23).AddMinutes(59));
            int soldierCurrentToday = await CalculateCurrentSoldiers(todayFromTs, todayToTs, checkInCamera, checkOutCamera, locationId);
            int guestCurrentToday = guestCount; // Số khách hiện tại bằng số khách trong ngày (DateCreated == today)

            // set giá trị để hiện ra table
            ViewData["SoldierTotal"] = soldierTotal;
            ViewData["SoldierCurrent"] = soldierCurrentToday;
            ViewData["GuestCount"] = guestCount;
            ViewData["GuestCurrent"] = guestCurrentToday;
            ViewBag.FromDate = parsedFromDate;
            ViewBag.ToDate = parsedToDate;
            ViewBag.Filter = filterType;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.Controller = this;

            // Lấy và thực thi dữ liệu event log, lọc thêm theo location_id và sourceID chỉ thuộc 2 camera
            var collection = _mongoService.GetCollection<eventLog>("EventLog");
            var filter = Builders<eventLog>.Filter.And(
                Builders<eventLog>.Filter.Gte(x => x.time_stamp, fromTimestamp),
                Builders<eventLog>.Filter.Lte(x => x.time_stamp, toTimestamp),
                Builders<eventLog>.Filter.Eq(x => x.locationId, locationId),
                Builders<eventLog>.Filter.In(x => x.sourceID, new[] { checkInCamera, checkOutCamera })
            );
            var totalRecords = await collection.CountDocumentsAsync(filter);
            var data = await collection.Find(filter)
                                      .SortBy(x => x.time_stamp)
                                      .Skip((page - 1) * pageSize)
                                      .Limit(pageSize)
                                      .ToListAsync();

            ViewBag.TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize);

            if (filterType != "CurrentGuests")
            {
                ProcessEventLog(data, checkInCamera, checkOutCamera, parsedFromDate, parsedToDate);

                // Lọc theo filterType
                if (filterType == "Late")
                {
                    data = data.Where(x => x.type_eventLE == "L").ToList();
                }
                else if (filterType == "Early")
                {
                    data = data.Where(x => x.type_eventLE == "E").ToList();
                }
                else if (filterType == "CurrentSoldiers")
                {
                    var currentSoldiers = await GetCurrentSoldiers(fromTimestamp, toTimestamp, checkInCamera, checkOutCamera, locationId);
                    data = currentSoldiers.Select(s => new eventLog
                    {
                        userGuid = s.UserGuid_cur,
                        Name = s.Name_cur,
                        idCard = s.IdCard_cur,
                        Gender = s.Gender_cur,
                        phone = s.PhoneNumber_cur
                    }).ToList();
                }
            }
            else
            {
                var currentGuests = await GetCurrentGuests(today);

                // Tạo danh sách eventLog giả từ danh sách khách
                data = currentGuests.Select(g => new eventLog
                {
                    Name = g.Name, // Giả sử Staff có thuộc tính Name
                    formatted_date = g.DateCreated.HasValue ? g.DateCreated.Value.ToString("dd/MM/yyyy HH:mm") : "N/A",
                    idCard = g.DocumentNumber,
                    Gender = g.Gender == 1 ? "Male" : "Female",
                }).ToList();

                ViewBag.TotalPages = (int)Math.Ceiling((double)data.Count / pageSize);
            }

            return View(data);
        }
        private (DateTime, DateTime, long, long) ParseDateRange(string fromDate, string toDate)
        {
            DateTime today = DateTime.Today;
            DateTime parsedFromDate = today;
            DateTime parsedToDate = today.AddHours(23).AddMinutes(59);

            if (!string.IsNullOrEmpty(fromDate) && DateTime.TryParseExact(fromDate, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var from))
            {
                parsedFromDate = from;
            }
            else if (!string.IsNullOrEmpty(fromDate) && DateTime.TryParseExact(fromDate, "dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out from))
            {
                parsedFromDate = from;
            }

            if (!string.IsNullOrEmpty(toDate) && DateTime.TryParseExact(toDate, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var to))
            {
                parsedToDate = to;
            }
            else if (!string.IsNullOrEmpty(toDate) && DateTime.TryParseExact(toDate, "dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out to))
            {
                parsedToDate = to;
            }

            long fromTimestamp = TimeStampHelper.ConvertToUnixTimestamp(parsedFromDate);
            long toTimestamp = TimeStampHelper.ConvertToUnixTimestamp(parsedToDate);

            return (parsedFromDate, parsedToDate, fromTimestamp, toTimestamp);
        }

        private CameraSettings LoadCameraSettings()
        {
            var settingcamera = Path.Combine(_env.ContentRootPath, "settingcamera.json");
            var cameraSettingsJson = System.IO.File.ReadAllText(settingcamera);
            return JsonSerializer.Deserialize<CameraSettings>(cameraSettingsJson);
        }

        private void SaveCurrentSoldiers(List<CurrentSoldier> soldiers)
        {
            var currentSoldiersFile = Path.Combine(_env.ContentRootPath, "currentsoldiers.json");
            var json = JsonSerializer.Serialize(soldiers, new JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(currentSoldiersFile, json);
        }
        public List<CurrentSoldier> LoadCurrentSoldiers()
        {
            var currentSoldiersFile = Path.Combine(_env.ContentRootPath, "currentsoldiers.json");
            if (!System.IO.File.Exists(currentSoldiersFile))
            {
                System.IO.File.WriteAllText(currentSoldiersFile, "[]");
            }
            var json = System.IO.File.ReadAllText(currentSoldiersFile);
            return JsonSerializer.Deserialize<List<CurrentSoldier>>(json) ?? new List<CurrentSoldier>();
        }


        private async Task<int> CalculateCurrentSoldiers(long fromTimestamp, long toTimestamp, string checkInCamera, string checkOutCamera, string locationId)
        {
            await UpdateCurrentSoldiersFromEvents(fromTimestamp, toTimestamp, checkInCamera, checkOutCamera, locationId);
            var currentSoldiers = LoadCurrentSoldiers();
            return currentSoldiers.Count;
        }
        private async Task UpdateCurrentSoldiersFromEvents(long fromTimestamp, long toTimestamp, string checkInCamera, string checkOutCamera, string locationId)
        {
            var staffList = await _context.Staff
                .Where(s => s.IdTypePerson.HasValue && (s.IdTypePerson.Value == 0 || s.IdTypePerson.Value == 2))
                .ToListAsync();
            var filter = Builders<eventLog>.Filter.And(
                Builders<eventLog>.Filter.Gte(x => x.time_stamp, fromTimestamp),
                Builders<eventLog>.Filter.Lte(x => x.time_stamp, toTimestamp),
                Builders<eventLog>.Filter.Eq(x => x.locationId, locationId),
                Builders<eventLog>.Filter.In(x => x.sourceID, new[] { checkInCamera, checkOutCamera })
            );

            var records = await _mongoService.GetCollection<eventLog>("EventLog")
                .Find(filter)
                .SortBy(x => x.time_stamp)
                .ToListAsync();

            var currentSoldiers = LoadCurrentSoldiers();

            foreach (var record in records)
            {
                var userGuid = record.userGuid;
                var staff = staffList.FirstOrDefault(s => s.GuidStaff == userGuid);

                // Kiểm tra nếu staff là null, bỏ qua bản ghi này
                if (staff == null)
                {
                    _logger.LogWarning("Không tìm thấy nhân viên với userGuid: {UserGuid} trong EventLog.", userGuid);
                    continue;
                }

                bool isCheckIn = record.sourceID == checkInCamera;
                var soldier = currentSoldiers.FirstOrDefault(s => s.UserGuid_cur == userGuid);

                if (isCheckIn)
                {
                    if (soldier == null)
                    {
                        currentSoldiers.Add(new CurrentSoldier
                        {
                            UserGuid_cur = userGuid,
                            Name_cur = staff.Name,
                            IdCard_cur = staff.DocumentNumber,
                            Gender_cur = staff.Gender == 1 ? "Male" : "Female",
                            PhoneNumber_cur = staff.Phone ?? "",
                        });
                    }
                }
                else
                {
                    if (soldier != null)
                    {
                        currentSoldiers.Remove(soldier);
                    }
                }
            }

            SaveCurrentSoldiers(currentSoldiers);
        }

        private async Task<List<CurrentSoldier>> GetCurrentSoldiers(long fromTimestamp, long toTimestamp, string checkInCamera, string checkOutCamera, string locationId)
        {
            await UpdateCurrentSoldiersFromEvents(fromTimestamp, toTimestamp, checkInCamera, checkOutCamera, locationId);
            return LoadCurrentSoldiers();
        }

        [HttpPost]

        public async Task<IActionResult> CheckIdCard(string idCard)
        {
            try
            {
                _logger.LogInformation("Checking idCard: {IdCard}", idCard);

                var staff = await _context.Staff
                    .FirstOrDefaultAsync(s => s.DocumentNumber == idCard && s.IdTypePerson.HasValue && (s.IdTypePerson.Value == 0 || s.IdTypePerson.Value == 2));

                if (staff == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy quân nhân với mã định danh này." });
                }

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        UserGuid_cur = staff.GuidStaff,
                        Name_cur = staff.Name,
                        IdCard_cur = staff.DocumentNumber,
                        Gender_cur = staff.Gender == 1 ? "Nam" : "Nữ",
                        PhoneNumber_cur = staff.Phone ?? ""
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi kiểm tra idCard: {IdCard}", idCard);
                return StatusCode(500, new { success = false, message = $"Lỗi server: {ex.Message}" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddCurrentSoldier(string userGuid, string name, string idCard, string gender, int typePerson, string phone)
        {
            // Kiểm tra thông tin đầu vào
            if (string.IsNullOrEmpty(userGuid) || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(idCard) ||
                string.IsNullOrEmpty(gender) || string.IsNullOrEmpty(phone))
            {
                return Json(new { success = false, message = "Thông tin không hợp lệ. Vui lòng nhập đầy đủ và đúng định dạng." });
            }
            // Kiểm tra idCard trong cơ sở dữ liệu
            var staff = _context.Staff.FirstOrDefault(s => s.DocumentNumber == idCard && s.IdTypePerson.HasValue && (s.IdTypePerson.Value == 0 || s.IdTypePerson.Value == 2));
            if (staff == null)
            {
                return Json(new { success = false, message = "Không tìm thấy quân nhân với mã định danh này." });
            }

            var currentSoldiers = LoadCurrentSoldiers();
            if (!currentSoldiers.Any(s => s.UserGuid_cur == userGuid))
            {
                currentSoldiers.Add(new CurrentSoldier
                {
                    UserGuid_cur = userGuid,
                    Name_cur = name,
                    IdCard_cur = idCard,
                    Gender_cur = gender,
                    PhoneNumber_cur = phone
                });
                SaveCurrentSoldiers(currentSoldiers);
                return Json(new { success = true, message = "Đã thêm quân nhân." });
            }
            return Json(new { success = false, message = "Quân nhân đã tồn tại." });
        }

        [HttpPost]
        public IActionResult RemoveCurrentSoldier(string userGuid)
        {
            var currentSoldiers = LoadCurrentSoldiers();
            var soldier = currentSoldiers.FirstOrDefault(s => s.UserGuid_cur == userGuid);
            if (soldier != null)
            {
                currentSoldiers.Remove(soldier);
                SaveCurrentSoldiers(currentSoldiers);

                TempData["Success"] = "Đã xóa quân nhân.";
                return Json(new { success = true, message = "Đã xóa quân nhân." });
            }
            TempData["Error"] = "Không tìm thấy quân nhân.";
            return Json(new { success = false, message = "Không tìm thấy quân nhân." });
        }

        [HttpPost]
        public async Task<IActionResult> InitializeCurrentSoldiers()
        {
            var soldiers = await _context.Staff
                .Where(s => s.IdTypePerson.HasValue && (s.IdTypePerson.Value == 0 || s.IdTypePerson.Value == 2))
                .ToListAsync();

            var currentSoldiers = soldiers.Select(staff => new CurrentSoldier
            {
                UserGuid_cur = staff.GuidStaff,
                Name_cur = staff.Name,
                IdCard_cur = staff.DocumentNumber,
                Gender_cur = staff.Gender == 1 ? "Male" : "Female",
                PhoneNumber_cur = staff.Phone ?? ""
            }).ToList();

            SaveCurrentSoldiers(currentSoldiers);
            return Ok("Đã khởi tạo danh sách quân số.");
        }

        private async Task<List<Staff>> GetCurrentGuests(DateTime dateFilter)
        {
            return await _context.Staff
                .Where(s => s.IdTypePerson.HasValue && s.IdTypePerson.Value == 3 && s.DateCreated.HasValue && s.DateCreated.Value.Date == dateFilter)
                .ToListAsync();
        }

        // Xử lý dữ liệu đã lấy (gán tên, loại sự kiện)
        private void ProcessEventLog(List<eventLog> data, string checkInCamera, string checkOutCamera, DateTime fromDate, DateTime toDate)
        {
            var sources = _context.Sources.Select(s => new { s.Guid, s.Name }).ToList();
            var lateThreshold = fromDate.Date.AddHours(8).AddMinutes(30);
            var earlyThreshold = toDate.Date.AddHours(17).AddMinutes(30);

            var groupedData = data
                .GroupBy(x => new { x.userGuid, Date = DateTimeOffset.FromUnixTimeSeconds(x.time_stamp).ToLocalTime().DateTime.Date })
                .ToList();

            _logger.LogInformation("Found {GroupCount} groups of events", groupedData.Count);

            foreach (var group in groupedData)
            {
                var userGuid = group.Key.userGuid;
                var date = group.Key.Date;
                var staff = _context.Staff.FirstOrDefault(s => s.GuidStaff == userGuid);
                bool exclude = staff != null && staff.IdTypePerson == 0;
                var userRecords = group.ToList();

                _logger.LogInformation("Processing userGuid: {UserGuid}, Date: {Date}, Records: {RecordCount}, Exclude: {Exclude}", userGuid, date, userRecords.Count, exclude);

                foreach (var item in userRecords)
                {
                    //so sánh id camera và gán name
                    var source = sources.FirstOrDefault(s => s.Guid == item.sourceID);
                    item.cameraGuid = item.sourceID;
                    item.cameraName = source?.Name ?? item.sourceID;
                    if (source != null)
                    {
                        item.sourceID = source.Name;
                    }
                    item.formatted_date = TimeStampHelper.ConvertTimestamp(item.time_stamp);

                    // phân loại nhận diện vào hay ra
                    if (item.cameraGuid == checkInCamera)
                        item.type_eventIO = "In";
                    else if (item.cameraGuid == checkOutCamera)
                        item.type_eventIO = "Out";
                    else
                        item.type_eventIO = "N/A";

                    item.type_eventLE = "";
                    item.IsLate = false;
                    item.IsLeaveEarly = false;

                    _logger.LogInformation("Event: userGuid={UserGuid}, time_stamp={TimeStamp}, sourceID={SourceID}, type_eventIO={TypeEventIO}", item.userGuid, item.time_stamp, item.sourceID, item.type_eventIO);
                }

                if (exclude)
                {
                    continue;
                }

                if (staff != null && staff.IdTypePerson == 2)
                {
                    // Lấy check-in đầu tiên trong ngày
                    var firstCheckIn = userRecords
                        .Where(x => x.type_eventIO == "In")
                        .OrderBy(x => x.time_stamp)
                        .FirstOrDefault();

                    // Gán mác "đi muộn" cho check-in đầu tiên nếu muộn hơn 8:30
                    if (firstCheckIn != null)
                    {
                        var checkInTime = DateTimeOffset.FromUnixTimeSeconds(firstCheckIn.time_stamp).ToLocalTime().DateTime;
                        if (checkInTime > lateThreshold)
                        {
                            firstCheckIn.type_eventLE = "L";
                            firstCheckIn.IsLate = true;
                            _logger.LogInformation("Marked as Late: userGuid={UserGuid}, checkInTime={CheckInTime}", userGuid, checkInTime);
                        }
                    }

                    // Lấy check-out cuối cùng trong ngày
                    var lastCheckOut = userRecords
                        .Where(x => x.type_eventIO == "Out")
                        .OrderByDescending(x => x.time_stamp)
                        .FirstOrDefault();

                    // Gán mác "về sớm" hoặc "check-out bình thường"
                    if (lastCheckOut != null)
                    {
                        var checkOutTime = DateTimeOffset.FromUnixTimeSeconds(lastCheckOut.time_stamp).ToLocalTime().DateTime;
                        if (checkOutTime < earlyThreshold)
                        {
                            // Kiểm tra xem có check-in nào sau check-out này nhưng trước 17:30 không
                            var hasLaterCheckIn = userRecords
                                .Any(x => x.type_eventIO == "In" &&
                                          x.time_stamp > lastCheckOut.time_stamp &&
                                          DateTimeOffset.FromUnixTimeSeconds(x.time_stamp).ToLocalTime().DateTime <= earlyThreshold);

                            if (!hasLaterCheckIn)
                            {
                                lastCheckOut.type_eventLE = "E";
                                lastCheckOut.IsLeaveEarly = true;
                            }
                            else
                            {
                                lastCheckOut.type_eventLE = "O"; // Check-out bình thường nếu có check-in sau
                            }
                        }
                        else
                        {
                            lastCheckOut.type_eventLE = "O"; // Check-out bình thường nếu sau 17:30
                        }
                    }
                }
            }
        }
        public List<ItemModel> getItemModel()
        {
            List<ItemModel> list = new List<ItemModel>();
            return list;
        }

        [HttpPost]
        public async Task<IActionResult> ExReport(string fromDate, string toDate, string note, string filterType = "All")
        {
            try
            {
                // Parse date range
                var (parsedFromDate, parsedToDate, fromTimestamp, toTimestamp) = ParseDateRange(fromDate, toDate);

                // Tải config camera
                var cameraSettings = LoadCameraSettings();
                var checkInCamera = cameraSettings.cameras.FirstOrDefault(c => c.type == "in")?.source_id;
                var checkOutCamera = cameraSettings.cameras.FirstOrDefault(c => c.type == "out")?.source_id;
                var locationId = cameraSettings.location_id;

                // Tính quân số và số khách
                DateTime today = DateTime.Today;
                int soldierTotal = await _context.Staff.CountAsync(s => s.IdTypePerson.HasValue && (s.IdTypePerson.Value == 0 || s.IdTypePerson.Value == 2));
                int guestCount = await _context.Staff.CountAsync(s => s.IdTypePerson.HasValue && s.IdTypePerson.Value == 3 && s.DateCreated.HasValue && s.DateCreated.Value.Date == today);
                int soldierCurrent = await CalculateCurrentSoldiers(fromTimestamp, toTimestamp, checkInCamera, checkOutCamera, locationId);
                int guestCurrent = guestCount; // Số khách hiện tại bằng số khách trong ngày

                // Truy vấn dữ liệu từ MongoDB
                List<eventLog> data;
                if (filterType != "CurrentGuests")
                {
                    var collection = _mongoService.GetCollection<eventLog>("EventLog");
                    var filter = Builders<eventLog>.Filter.And(
                        Builders<eventLog>.Filter.Gte(x => x.time_stamp, fromTimestamp),
                        Builders<eventLog>.Filter.Lte(x => x.time_stamp, toTimestamp),
                        Builders<eventLog>.Filter.Eq(x => x.locationId, locationId),
                        Builders<eventLog>.Filter.In(x => x.sourceID, new[] { checkInCamera, checkOutCamera })
                    );

                    data = await collection.Find(filter)
                                           .SortBy(x => x.time_stamp)
                                           .ToListAsync(); // Lấy toàn bộ, không phân trang

                    // Xử lý dữ liệu (gán tên, loại sự kiện, đi muộn/về sớm)
                    ProcessEventLog(data, checkInCamera, checkOutCamera, parsedFromDate, parsedToDate);

                    // Lọc theo filterType
                    if (filterType == "Late")
                    {
                        data = data.Where(x => x.type_eventLE == "L").ToList();
                    }
                    else if (filterType == "Early")
                    {
                        data = data.Where(x => x.type_eventLE == "E").ToList();
                    }
                    else if (filterType == "CurrentSoldiers")
                    {
                        var currentSoldiers = LoadCurrentSoldiers();
                        data = currentSoldiers.Select(s => new eventLog
                        {
                            Name = s.Name_cur,
                            idCard = s.IdCard_cur,
                            Gender = s.Gender_cur,
                            phone = s.PhoneNumber_cur
                        }).ToList();
                    }
                }
                else
                {
                    var currentGuests = await GetCurrentGuests(today);
                    data = currentGuests.Select(g => new eventLog
                    {
                        Name = g.Name,
                        formatted_date = g.DateCreated.HasValue ? g.DateCreated.Value.ToString("dd/MM/yyyy HH:mm") : "N/A",
                        idCard = g.DocumentNumber,
                        Gender = g.Gender == 1 ? "Male" : "Female"
                    }).ToList();
                }

                // đổi dữ liệu sang ItemModel để xuất Excel
                List<ItemModel> itemList;
                if (filterType == "CurrentGuests")
                {
                    itemList = data.Select(item => new ItemModel
                    {
                        Name = item.Name ?? "Unknown",
                        CheckTime = item.formatted_date ?? "N/A",
                        IdCard = item.idCard ?? "N/A",
                        Gender = item.Gender ?? "N/A"
                    }).ToList();
                }
                else if (filterType == "CurrentSoldiers")
                {
                    itemList = data.Select(item => new ItemModel
                    {
                        Name = item.Name ?? "Unknown",
                        IdCard = item.idCard ?? "N/A",
                        Gender = item.Gender ?? "N/A",
                        Phone_number = item.phone ?? "N/A"
                    }).ToList();
                }
                else
                {
                    itemList = data.Select(item => new ItemModel
                    {
                        Name = item.Name,
                        CheckTime = item.formatted_date,
                        CheckType = item.type_eventIO,
                        Source = item.sourceID,
                        EndTime = item.type_eventLE
                    }).ToList();
                }

                // Định nghĩa tên file và đường dẫn nêu khách thì dùng mẫu khách
                var folder = @"D:\excel";
                string fileName;
                string templatePath;

                if (filterType == "CurrentGuests")
                {
                    fileName = $"CurrentGuests_Report_{DateTime.Now.ToString("yyyyMMdd_HHmmss")}.xlsx";
                    templatePath = Path.Combine(folder, "_TemplateGuests.xlsx");
                }
                else if (filterType == "CurrentSoldiers")
                {
                    fileName = $"CurrentSoldiers_Report_{DateTime.Now.ToString("yyyyMMdd_HHmmss")}.xlsx";
                    templatePath = Path.Combine(folder, "_TemplateSoldiers.xlsx"); // Sửa tên template ở đây
                }
                else
                {
                    // Nếu không phải là "CurrentGuests" hay "CurrentSoldiers"
                    fileName = $"Attendance_Report_{DateTime.Now.ToString("yyyyMMdd_HHmmss")}.xlsx"; // Tên file chung
                    templatePath = Path.Combine(folder, "_TemplateWeb78.xlsx");
                }

                // biến lưu data cho vào file mới
                var stream = new MemoryStream();

                using (var package = new ExcelPackage(new FileInfo(templatePath)))
                {
                    var worksheet = package.Workbook.Worksheets.FirstOrDefault();
                    if (worksheet == null)
                        return StatusCode(500, "Không tìm thấy worksheet trong file template.");

                    // phạm vi thời gian
                    var startDate = DateTime.ParseExact(fromDate, "dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture);
                    var endDate = DateTime.ParseExact(toDate, "dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture);

                    // ghi phạm vi thời gian
                    worksheet.Cells["C2"].Value = $"{startDate:dd-MM-yyyy HH:mm} - {endDate:dd-MM-yyyy HH:mm}";
                    // ghi quân số, số khách
                    worksheet.Cells["C3"].Value = $"{soldierCurrent} / {soldierTotal}";
                    worksheet.Cells["E3"].Value = $"{guestCount}";
                    // ghi loại tìm kiếm
                    worksheet.Cells["C4"].Value = $"{filterType}";

                    // Ghi chú
                    worksheet.Cells["C5"].Value = string.IsNullOrWhiteSpace(note) ? "Không có ghi chú" : note;

                    // ghi dữ liệu
                    for (int i = 0; i < itemList.Count; i++)
                    {
                        var row = i + 7;
                        worksheet.Cells[row, 1].Value = (i + 1).ToString();
                        worksheet.Cells[row, 2].Value = itemList[i].Name;
                        if (filterType == "CurrentGuests")
                        {
                            worksheet.Cells[row, 3].Value = itemList[i].CheckTime;
                            worksheet.Cells[row, 4].Value = itemList[i].IdCard;
                            worksheet.Cells[row, 5].Value = itemList[i].Gender;
                        }
                        else if (filterType == "CurrentSoldiers")
                        {
                            worksheet.Cells[row, 3].Value = itemList[i].IdCard;
                            worksheet.Cells[row, 4].Value = itemList[i].Gender;
                            worksheet.Cells[row, 5].Value = itemList[i].Phone_number;
                        }
                        else
                        {
                            worksheet.Cells[row, 3].Value = itemList[i].CheckTime;
                            worksheet.Cells[row, 4].Value = itemList[i].CheckType;
                            worksheet.Cells[row, 5].Value = itemList[i].Source;
                            //worksheet.Cells[row, 6].Value = data[i].EndTime == "L" ? "Đi muộn" : data[i].EndTime == "E" ? "Về sớm" : data[i].EndTime; ;
                        }
                    }
                    package.SaveAs(stream);
                }
                // xuất file excel
                stream.Position = 0;

                return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);

            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi xuất file: {ex.Message}");
            }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}