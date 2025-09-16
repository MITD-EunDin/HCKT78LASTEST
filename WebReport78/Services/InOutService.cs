using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WebReport78.Models;
using WebReport78.Repositories;

namespace WebReport78.Services
{
    public class InOutService : IInOutService
    {
        private readonly IStaffRepository _staffRepo;
        private readonly IEventLogRepository _eventLogRepo;
        private readonly IJsonFileService _jsonService;
        private readonly ILogger<InOutService> _logger;

        public InOutService(IStaffRepository staffRepo, IEventLogRepository eventLogRepo, IJsonFileService jsonService, ILogger<InOutService> logger)
        {
            _staffRepo = staffRepo;
            _eventLogRepo = eventLogRepo;
            _jsonService = jsonService;
            _logger = logger;
        }

        private (long fromTsToday, long toTsToday) GetTodayTimestampRange()
        {
            var todayStart = DateTime.Today;  // Đầu ngày hôm nay (00:00)
            var now = DateTime.Now;           // Thời điểm hiện tại
            long fromTsToday = TimeStampHelper.ConvertToUnixTimestamp(todayStart);
            long toTsToday = TimeStampHelper.ConvertToUnixTimestamp(now);
            return (fromTsToday, toTsToday);
        }

        public (DateTime, DateTime, long, long) ParseDateRange(string fromDateStr, string toDateStr)
        {
            // Các định dạng ngày giờ hỗ trợ
            var formats = new[] { "yyyy-MM-ddTHH:mm", "dd-MM-yyyy HH:mm" };
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            DateTime fromDate = DateTime.Today;
            DateTime toDate = DateTime.Today.AddHours(23).AddMinutes(59);

            foreach (var format in formats)
            {
                if (DateTime.TryParseExact(fromDateStr, format, culture, System.Globalization.DateTimeStyles.None, out var parsedFrom))
                    fromDate = parsedFrom;
                if (DateTime.TryParseExact(toDateStr, format, culture, System.Globalization.DateTimeStyles.None, out var parsedTo))
                    toDate = parsedTo;
            }

            long fromTs = TimeStampHelper.ConvertToUnixTimestamp(fromDate);
            long toTs = TimeStampHelper.ConvertToUnixTimestamp(toDate);
            return (fromDate, toDate, fromTs, toTs);
        }

        // sô lượng quân sô hiện tại
        public async Task<int> CalculateCurrentSoldiersAsync(long fromTs, long toTs, string locationId)
        {
            await UpdateCurrentSoldiersFromEventsAsync(fromTs, toTs, locationId);
            return _jsonService.LoadCurrentSoldiers().Count;
        }

        // cập nhật quân số hiện tại
        public async Task UpdateCurrentSoldiersFromEventsAsync(long fromTs, long toTs, string locationId)
        {
            var staffList = await _staffRepo.GetStaffListAsync();
            var vehicles = await _staffRepo.GetVehiclesAsync();
            var sources = await _staffRepo.GetSourcesAsync();

            var records = await _eventLogRepo.GetEventLogsAsync(fromTs, toTs, locationId, 1, int.MaxValue);
            var currentSoldiers = _jsonService.LoadCurrentSoldiers();
            var manualActions = _jsonService.LoadManualActions();

            Parallel.ForEach(records, record =>
            {
                // Thêm kiểm tra event_name trước khi xử lý
                if (record.Name == "Unknown" || string.IsNullOrEmpty(record.Name))
                {
                    return; // Bỏ qua bản ghi này
                }

                string key = record.typeEvent == 1 ? record.userGuid : record.Name;
                if (string.IsNullOrEmpty(key)) return;

                var source = sources.FirstOrDefault(s => s.Guid == record.sourceID);
                if (source == null || (source.AcCheckType != 1 && source.AcCheckType != 2)) return;

                bool isCheckIn = source.AcCheckType == 2;
                var staff = GetStaffFromUserGuidAsync(key).Result; // Dùng Result trong Parallel
                if (staff == null) return;

                var userGuid = staff.GuidStaff;
                lock (manualActions)
                {
                    var manual = manualActions.FirstOrDefault(m => m.UserGuid == userGuid);
                    if (manual == null)
                    {
                        manual = new ManualAction { UserGuid = userGuid };
                        manualActions.Add(manual);
                    }

                    bool apply = !manual.LastActionTimestamp.HasValue || record.time_stamp > manual.LastActionTimestamp.GetValueOrDefault();
                    if (!apply) return;

                    lock (currentSoldiers)
                    {
                        if (isCheckIn)
                        {
                            if (!currentSoldiers.Any(s => s.UserGuid_cur == userGuid))
                            {
                                currentSoldiers.Add(new CurrentSoldier
                                {
                                    UserGuid_cur = userGuid,
                                    Name_cur = staff.Name,
                                    IdCard_cur = staff.DocumentNumber ?? "N/A",
                                    Gender_cur = staff.Gender == 1 ? "Nam" : "Nữ",
                                    PhoneNumber_cur = staff.Phone ?? ""
                                });
                            }
                            manual.LastActionType = 2;
                        }
                        else
                        {
                            var soldier = currentSoldiers.FirstOrDefault(s => s.UserGuid_cur == userGuid);
                            if (soldier != null) currentSoldiers.Remove(soldier);
                            manual.LastActionType = 1;
                        }
                        manual.LastActionTimestamp = record.time_stamp;
                    }
                }
            });

            Parallel.ForEach(manualActions, manual =>
            {
                if (manual.LastActionType == 2 && !currentSoldiers.Any(s => s.UserGuid_cur == manual.UserGuid))
                {
                    var staff = GetStaffFromUserGuidAsync(manual.UserGuid).Result;
                    if (staff != null)
                    {
                        lock (currentSoldiers)
                        {
                            currentSoldiers.Add(new CurrentSoldier
                            {
                                UserGuid_cur = manual.UserGuid,
                                Name_cur = staff.Name,
                                IdCard_cur = staff.DocumentNumber ?? "N/A",
                                Gender_cur = staff.Gender == 1 ? "Nam" : "Nữ",
                                PhoneNumber_cur = staff.Phone ?? ""
                            });
                        }
                    }
                }
            });

            _jsonService.SaveCurrentSoldiers(currentSoldiers);
            _jsonService.SaveManualActions(manualActions);
        }

        // danh sách quân số hiện tại
        public async Task<List<CurrentSoldier>> GetCurrentSoldiersAsync(long fromTs, long toTs, string locationId)
        {
            await UpdateCurrentSoldiersFromEventsAsync(fromTs, toTs, locationId);
            return _jsonService.LoadCurrentSoldiers();
        }

        // danh sách khách theo ngày tìm kiếm
        public async Task<List<Staff>> GetCurrentGuestsAsync(DateTime fromDate, DateTime toDate)
        {
            long fromTs = TimeStampHelper.ConvertToUnixTimestamp(fromDate);
            long toTs = TimeStampHelper.ConvertToUnixTimestamp(toDate);
            return await _staffRepo.GetGuestsAsync(fromTs, toTs);
        }

        // lấy thông tin nhân viên từ mongo theo sự kiện 1 và 25
        public async Task<Staff> GetStaffFromUserGuidAsync(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;

            var staffList = await _staffRepo.GetStaffListAsync();
            var vehicles = await _staffRepo.GetVehiclesAsync();
            string matchType = "none";

            if (staffList.Any(s => s.GuidStaff == key))
                matchType = "staff";
            else if (vehicles.Any(v => v.Lpn == key && v.IdStaff != null))
                matchType = "vehicle";

            Staff staff = null;
            switch (matchType)
            {
                case "staff":
                    staff = staffList.FirstOrDefault(s => s.GuidStaff == key);
                    break;
                case "vehicle":
                    var vehicle = vehicles.FirstOrDefault(v => v.Lpn == key);
                    if (vehicle != null)
                        staff = staffList.FirstOrDefault(s => s.GuidStaff == vehicle.IdStaff);
                    break;
            }
            return staff;
        }

        // xử lý dữ liệu log từ mongo
        public async Task ProcessEventLogAsync(List<eventLog> data, DateTime fromDate, DateTime toDate)
        {
            var sources = await _staffRepo.GetSourcesAsync();
            var staffList = await _staffRepo.GetStaffListAsync();
            var vehicles = await _staffRepo.GetVehiclesAsync();
            var manualActions = _jsonService.LoadManualActions();

            var lateThreshold = fromDate.Date.AddHours(7).AddMinutes(30);
            var earlyThreshold = toDate.Date.AddHours(16).AddMinutes(30);

            // Tối ưu: Sử dụng Parallel.ForEach nếu data lớn, cần kiểm tra concurrency
            Parallel.ForEach(data, item =>
            {
                var source = sources.FirstOrDefault(s => s.Guid == item.sourceID);
                if (source == null || (source.AcCheckType != 1 && source.AcCheckType != 2)) return;

                item.cameraGuid = item.sourceID;
                item.cameraName = source.Name ?? item.sourceID;
                item.sourceID = source.Name ?? item.sourceID;
                item.formatted_date = TimeStampHelper.ConvertTimestamp(item.time_stamp);
                item.type_eventIO = source.AcCheckType == 2 ? "Check-In" : "Check-Out";

                // nếu fr thì so sách guid còn lpr thì lấy Name so sánh
                string key = item.typeEvent == 1 ? item.userGuid : item.Name;
                var staff = GetStaffFromUserGuidAsync(key).Result; // Dùng Result vì Parallel, nhưng tốt hơn dùng async full nếu cần
                if (staff == null) return;

                item.Name = staff.Name;
                item.idCard = staff.DocumentNumber ?? "N/A";
                item.Gender = staff.Gender == 1 ? "Nam" : "Nữ";
                item.phone = staff.Phone ?? "";
                if (item.typeEvent == 25) item.idCard = key;
            });

            CalculateOutTime(data);

            // Tối ưu group bằng LINQ
            var groupedData = data.GroupBy(x => new { Key = x.typeEvent == 1 ? x.userGuid : (vehicles.FirstOrDefault(v => v.Lpn == x.Name)?.IdStaff ?? x.Name), Date = DateTimeOffset.FromUnixTimeSeconds(x.time_stamp).ToLocalTime().DateTime.Date }).ToList();

            // Thay foreach bằng for loop để tối ưu nếu groupedData lớn
            for (int i = 0; i < groupedData.Count; i++)
            {
                var group = groupedData[i];
                var key = group.Key.Key;
                var date = group.Key.Date;
                if (string.IsNullOrEmpty(key)) continue;

                var staff = await GetStaffFromUserGuidAsync(key);
                if (staff == null) continue;

                bool exclude = staff.IdTypePerson == 0;
                var userRecords = group.ToList();

                if (exclude) continue;

                if (staff.IdTypePerson == 2)
                {
                    // LINQ để lấy firstCheckIn, lastCheckOut, hasLaterCheckIn - tránh lặp thủ công
                    var firstCheckIn = userRecords.Where(x => x.type_eventIO == "Check-In").OrderBy(x => x.time_stamp).FirstOrDefault();
                    if (firstCheckIn != null)
                    {
                        var checkInTime = DateTimeOffset.FromUnixTimeSeconds(firstCheckIn.time_stamp).ToLocalTime().DateTime;
                        if (checkInTime > lateThreshold)
                        {
                            firstCheckIn.type_eventLE = "L";
                            firstCheckIn.IsLate = true;
                        }
                    }

                    var lastCheckOut = userRecords.Where(x => x.type_eventIO == "Check-Out").OrderByDescending(x => x.time_stamp).FirstOrDefault();
                    if (lastCheckOut != null)
                    {
                        var checkOutTime = DateTimeOffset.FromUnixTimeSeconds(lastCheckOut.time_stamp).ToLocalTime().DateTime;
                        if (checkOutTime < earlyThreshold)
                        {
                            var hasLaterCheckIn = userRecords.Any(x => x.type_eventIO == "Check-In" && x.time_stamp > lastCheckOut.time_stamp && DateTimeOffset.FromUnixTimeSeconds(x.time_stamp).ToLocalTime().DateTime <= earlyThreshold);
                            if (!hasLaterCheckIn)
                            {
                                lastCheckOut.type_eventLE = "E";
                                lastCheckOut.IsLeaveEarly = true;
                            }
                            else
                            {
                                lastCheckOut.type_eventLE = "O";
                            }
                        }
                        else
                        {
                            lastCheckOut.type_eventLE = "O";
                        }
                    }
                }
            }
        }

        // tính thời gian ra ngoài
        public void CalculateOutTime(List<eventLog> data)
        {
            const int thresholdMinutes = 10; // ngưỡng cho phép 
            var checkPoint = new Dictionary<string, long>();
            var sortedData = data.OrderBy(x => x.time_stamp).ToList(); // sắp xếp
            for (int i = 0; i < sortedData.Count; i++)
            {
                var item = sortedData[i];
                if (item.type_eventIO == "Check-Out")
                {
                    if (!checkPoint.ContainsKey(item.userGuid))
                        checkPoint[item.userGuid] = item.time_stamp;
                }
                else if (item.type_eventIO == "Check-In")
                {
                    //if (checkPoint.TryGetValue(item.userGuid, out var startTime))
                    //{
                    //    var outTime = item.time_stamp - startTime;
                    //    var durationInMinutes = (int)TimeSpan.FromSeconds(outTime).TotalMinutes;
                    //    item.count_duration = $"{durationInMinutes} phút";
                    //    checkPoint.Remove(item.userGuid);
                    //}
                    if (checkPoint.TryGetValue(item.userGuid, out var startTime))
                    {
                        var startDate = DateTimeOffset.FromUnixTimeSeconds(startTime).ToLocalTime().Date;

                        // Chỉ tính nếu Check-Out và Check-In cùng ngày

                        var outTimeSeconds = item.time_stamp - startTime;
                        var durationInMinutes = (int)TimeSpan.FromSeconds(outTimeSeconds).TotalMinutes;

                        // Chỉ gán count_duration nếu thời gian ra ngoài >= ngưỡng A
                        if (durationInMinutes >= thresholdMinutes)
                        {
                            item.count_duration = $"{durationInMinutes} phút";
                        }


                        // Xóa checkpoint sau khi xử lý (dù có gán count_duration hay không)
                        checkPoint.Remove(item.userGuid);
                    }
                }
            }
        }

        public async Task<(int soldierTotal, int soldierCurrent, int guestCount, int guestCurrent)> GetSummaryAsync(long fromTs, long toTs, string locationId, DateTime fromDate)
        {
            var soldierTotal = await _staffRepo.GetSoldierTotalAsync();
            int soldierCurrent;

            if (fromDate.Date != DateTime.Today)
            {
                // Nếu fromDate không phải hôm nay, load từ JSON mà không tính lại
                soldierCurrent = _jsonService.LoadCurrentSoldiers().Count;
            }
            else
            {
                // Nếu fromDate là hôm nay, tính toán như bình thường
                var (fromTsToday, toTsToday) = GetTodayTimestampRange();
                soldierCurrent = await CalculateCurrentSoldiersAsync(fromTsToday, toTsToday, locationId);
            }

            var guests = await _staffRepo.GetGuestsAsync(fromTs, toTs);
            var guestCount = guests.Count;
            var guestCurrent = await _staffRepo.GetGuestCurrentTodayAsync(guests, toTs);
            return (soldierTotal, soldierCurrent, guestCount, guestCurrent);
        }

        // thêm quân số hiện tại thủ công
        public async Task AddCurrentSoldierAsync(string userGuid, string name, string idCard, string gender, string phone)
        {
            if (string.IsNullOrEmpty(userGuid) || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(idCard) ||
                string.IsNullOrEmpty(gender) || string.IsNullOrEmpty(phone))
                throw new ArgumentException("Invalid input data");

            var staff = await _staffRepo.GetStaffByDocumentNumberAsync(idCard);
            if (staff == null) throw new InvalidOperationException("Staff not found with provided ID card");

            var currentSoldiers = _jsonService.LoadCurrentSoldiers();
            var manualActions = _jsonService.LoadManualActions();
            var existingSoldier = currentSoldiers.FirstOrDefault(s => s.UserGuid_cur == userGuid);
            var manual = manualActions.FirstOrDefault(m => m.UserGuid == userGuid);
            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            bool apply = manual == null || !manual.LastActionTimestamp.HasValue || currentTime > manual.LastActionTimestamp.GetValueOrDefault();
            if (!apply) throw new InvalidOperationException("Action not applied due to newer event");

            if (existingSoldier == null)
            {
                currentSoldiers.Add(new CurrentSoldier
                {
                    UserGuid_cur = userGuid,
                    Name_cur = name,
                    IdCard_cur = idCard,
                    Gender_cur = gender,
                    PhoneNumber_cur = phone
                });
            }

            if (manual == null)
            {
                manual = new ManualAction { UserGuid = userGuid };
                manualActions.Add(manual);
            }
            manual.LastActionTimestamp = currentTime;
            manual.LastActionType = 2;

            _jsonService.SaveCurrentSoldiers(currentSoldiers);
            _jsonService.SaveManualActions(manualActions);
        }

        // xóa quân số hiện tại thủ công
        public async Task RemoveCurrentSoldierAsync(string userGuid)
        {
            var currentSoldiers = _jsonService.LoadCurrentSoldiers();
            var manualActions = _jsonService.LoadManualActions();
            var soldier = currentSoldiers.FirstOrDefault(s => s.UserGuid_cur == userGuid);
            var manual = manualActions.FirstOrDefault(m => m.UserGuid == userGuid);
            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            bool apply = manual == null || !manual.LastActionTimestamp.HasValue || currentTime > manual.LastActionTimestamp.GetValueOrDefault();
            if (!apply) throw new InvalidOperationException("Action not applied due to newer event");

            if (soldier != null) currentSoldiers.Remove(soldier);

            if (manual == null)
            {
                manual = new ManualAction { UserGuid = userGuid };
                manualActions.Add(manual);
            }
            manual.LastActionTimestamp = currentTime;
            manual.LastActionType = 1;

            _jsonService.SaveCurrentSoldiers(currentSoldiers);
            _jsonService.SaveManualActions(manualActions);
        }

        public async Task InitializeCurrentSoldiersAsync()
        {
            var soldiers = await _staffRepo.GetStaffListAsync();
            var currentSoldiers = soldiers.Select(staff => new CurrentSoldier
            {
                UserGuid_cur = staff.GuidStaff,
                Name_cur = staff.Name,
                IdCard_cur = staff.DocumentNumber ?? "N/A",
                Gender_cur = staff.Gender == 1 ? "Nam" : "Nữ",
                PhoneNumber_cur = staff.Phone ?? ""
            }).ToList();

            _jsonService.SaveCurrentSoldiers(currentSoldiers);
        }

        // check cccd để thêm thủ công
        public async Task<Staff> CheckIdCardAsync(string idCard)
        {
            return await _staffRepo.GetStaffByDocumentNumberAsync(idCard);
        }

        public async Task<List<eventLog>> GetFilteredDataAsync(string filterType, long fromTs, long toTs, string locationId, DateTime fromDate, DateTime toDate, List<string> validSources)
        {
            if (filterType == "CurrentSoldiers")
            {
                if (fromDate.Date != DateTime.Today)
                {
                    // Nếu fromDate không phải hôm nay, trả về currentSoldiers từ JSON mà không tính toán lại
                    var currentSoldiers = _jsonService.LoadCurrentSoldiers();
                    return currentSoldiers.Select(s => new eventLog
                    {
                        userGuid = s.UserGuid_cur,
                        Name = s.Name_cur,
                        idCard = s.IdCard_cur,
                        Gender = s.Gender_cur,
                        phone = s.PhoneNumber_cur
                    }).ToList();
                }
                else
                {
                    // Nếu fromDate là hôm nay, tính toán CurrentSoldiers như bình thường
                    var (fromTsToday, toTsToday) = GetTodayTimestampRange();
                    await UpdateCurrentSoldiersFromEventsAsync(fromTsToday, toTsToday, locationId);
                    var currentSoldiers = _jsonService.LoadCurrentSoldiers();
                    return currentSoldiers.Select(s => new eventLog
                    {
                        userGuid = s.UserGuid_cur,
                        Name = s.Name_cur,
                        idCard = s.IdCard_cur,
                        Gender = s.Gender_cur,
                        phone = s.PhoneNumber_cur
                    }).ToList();
                }
            }

            var data = await _eventLogRepo.GetEventLogsAsync(fromTs, toTs, locationId, 1, int.MaxValue);
            data = data.Where(x => x.typeEvent == 1 || (x.typeEvent == 25 && validSources.Contains(x.sourceID))).ToList();

            if (filterType != "CurrentSoldiers") await ProcessEventLogAsync(data, fromDate, toDate);

            if (filterType == "Late") return data.Where(x => x.type_eventLE == "L").ToList();
            if (filterType == "Early") return data.Where(x => x.type_eventLE == "E").ToList();
            //if (filterType == "CurrentSoldiers")
            //{
            //    var currentSoldiers = await GetCurrentSoldiersAsync(fromTs, toTs, locationId);
            //    return currentSoldiers.Select(s => new eventLog
            //    {
            //        userGuid = s.UserGuid_cur,
            //        Name = s.Name_cur,
            //        idCard = s.IdCard_cur,
            //        Gender = s.Gender_cur,
            //        phone = s.PhoneNumber_cur
            //    }).ToList();
            //}
            return data;
        }

        // first check in và last check out
        public async Task<Dictionary<string, (DateTime? FirstIn, DateTime? LastOut, string CameraName)>> DoubleInOutAsync(long fromTs, long toTs, string locationId, string employeeGuid)
        {
            var result = new Dictionary<string, (DateTime? FirstIn, DateTime? LastOut, string CameraName)>();
            var records = await _eventLogRepo.GetEventLogsAsync(fromTs, toTs, locationId, 1, int.MaxValue);
            var sources = await _staffRepo.GetSourcesAsync();
            var staffList = await _staffRepo.GetStaffListAsync();
            var vehicles = await _staffRepo.GetVehiclesAsync();

            var filteredRecords = string.IsNullOrEmpty(employeeGuid)
                ? records
                : records.Where(r => r.userGuid == employeeGuid || (r.typeEvent == 25 && vehicles.FirstOrDefault(v => v.Lpn == r.Name)?.IdStaff == employeeGuid)).ToList();

            var groupedRecords = filteredRecords
                .GroupBy(r =>
                {
                    if (r.typeEvent == 1)
                        return r.userGuid;
                    var vehicle = vehicles.FirstOrDefault(v => v.Lpn == r.Name);
                    return vehicle?.IdStaff ?? r.userGuid;
                })
                .Where(g => !string.IsNullOrEmpty(g.Key));

            foreach (var group in groupedRecords)
            {
                var guid = group.Key;
                var staff = staffList.FirstOrDefault(s => s.GuidStaff == guid);
                if (staff == null) continue;

                var checkInRecords = group
                    .Where(r => sources.FirstOrDefault(s => s.Guid == r.sourceID)?.AcCheckType == 2)
                    .OrderBy(r => r.time_stamp)
                    .ToList();
                var checkOutRecords = group
                    .Where(r => sources.FirstOrDefault(s => s.Guid == r.sourceID)?.AcCheckType == 1)
                    .OrderByDescending(r => r.time_stamp)
                    .ToList();

                var firstCheckIn = checkInRecords.FirstOrDefault();
                var lastCheckOut = checkOutRecords.FirstOrDefault();

                var firstInTime = firstCheckIn != null
                    ? DateTimeOffset.FromUnixTimeSeconds(firstCheckIn.time_stamp).ToLocalTime().DateTime
                    : (DateTime?)null;
                var lastOutTime = lastCheckOut != null
                    ? DateTimeOffset.FromUnixTimeSeconds(lastCheckOut.time_stamp).ToLocalTime().DateTime
                    : (DateTime?)null;
                var cameraName = firstCheckIn != null
                    ? (sources.FirstOrDefault(s => s.Guid == firstCheckIn.sourceID)?.Name ?? firstCheckIn.sourceID)
                    : (lastCheckOut != null ? (sources.FirstOrDefault(s => s.Guid == lastCheckOut.sourceID)?.Name ?? lastCheckOut.sourceID) : "N/A");

                result[guid] = (firstInTime, lastOutTime, cameraName);
            }

            return result;
        }
    }
}