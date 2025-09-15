using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using OfficeOpenXml;
using WebReport78.Models;
using WebReport78.Repositories;

namespace WebReport78.Services
{
    public class LprService : ILprService
    {
        private readonly IStaffRepository _staffRepo;
        private readonly IInOutService _inOutService;
        private readonly IJsonFileService _jsonService;
        private readonly IEventLogRepository _eventLogRepo;
        private readonly ILogger<LprService> _logger;
        private readonly IWebHostEnvironment _env;

        public LprService(
            IStaffRepository staffRepo,
            IInOutService inOutService,
            IJsonFileService jsonService,
            IEventLogRepository eventLogRepo,
            ILogger<LprService> logger,
            IWebHostEnvironment env)
        {
            _staffRepo = staffRepo;
            _inOutService = inOutService;
            _jsonService = jsonService;
            _eventLogRepo = eventLogRepo;
            _logger = logger;
            _env = env;
        }

        public async Task<List<eventLog>> GetLprEventLogsAsync(long fromTs, long toTs, string locationId, int page, int pageSize)
        {
            try
            {
                var data = await _eventLogRepo.GetEventLogsAsync(fromTs, toTs, locationId, page, pageSize, 101);
                _logger.LogInformation($"Fetched {data.Count} LPR event logs for typeEvent 101, fromTs: {fromTs}, toTs: {toTs}, locationId: {locationId}");
                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching LPR event logs for typeEvent 101, fromTs: {fromTs}, toTs: {toTs}, locationId: {locationId}");
                throw;
            }
        }

        //public async Task<List<LprEventViewModel>> ProcessLprEventsAsync(List<eventLog> data)
        //{
        //    var sources = await _staffRepo.GetSourcesAsync();
        //    var vehicles = await _staffRepo.GetVehiclesAsync();
        //    var vehicleDict = vehicles.ToDictionary(v => v.Lpn, v => v);
        //    var sourceDict = sources.ToDictionary(s => s.Guid, s => s);
        //    var viewModel = new List<LprEventViewModel>();

        //    foreach (var item in data)
        //    {
        //        string licensePlate = null;
        //        try
        //        {
        //            var payload = JsonSerializer.Deserialize<Dictionary<string, string>>(item.payload); // Sửa playLoad thành payload
        //            if (payload == null || !payload.TryGetValue("Lpr", out licensePlate) || string.IsNullOrEmpty(licensePlate) || licensePlate == "Unknown")
        //            {
        //                _logger.LogWarning($"Skipping event {item.time_stamp} with invalid or missing license plate");
        //                continue;
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            _logger.LogWarning(ex, $"Failed to parse payload for event {item.time_stamp}");
        //            continue;
        //        }

        //        var vehicle = vehicleDict.ContainsKey(licensePlate) ? vehicleDict[licensePlate] : null;
        //        var source = sourceDict.ContainsKey(item.sourceID) ? sourceDict[item.sourceID] : null;

        //        if (vehicle == null || string.IsNullOrEmpty(item.Name))
        //        {
        //            _logger.LogWarning($"Skipping event {item.time_stamp} with missing vehicle or driver name");
        //            continue;
        //        }

        //        viewModel.Add(new LprEventViewModel
        //        {
        //            Timestamp = item.time_stamp,
        //            formatted_date = TimeStampHelper.ConvertTimestamp(item.time_stamp),
        //            LicensePlate = licensePlate,
        //            Owner = vehicle.Owner ?? "N/A",
        //            DirverName = item.Name, 
        //            CameraFr = source?.Name ?? item.sourceID,
        //            Warning = "Không khớp"
        //        });
        //    }

        //    _logger.LogInformation($"Processed {viewModel.Count} valid LPR events");
        //    return viewModel;
        //}

        public async Task<List<LprEventViewModel>> ProcessLprEventsAsync(List<eventLog> data)
        {
            var sources = await _staffRepo.GetSourcesAsync();
            var vehicles = await _staffRepo.GetVehiclesAsync();
            var viewModel = new List<LprEventViewModel>();

            foreach (var item in data)
            {
                try
                {
                    // Lấy biển số từ payload
                    string licensePlate = null;
                    var payload = JsonSerializer.Deserialize<Dictionary<string, object>>(item.payload);
                    if (!payload.TryGetValue("Lpr", out var licensePlateObj))
                    {
                        _logger.LogWarning($"Không tìm thấy biển số trong payload của sự kiện {item.time_stamp}");
                        continue;
                    }

                    licensePlate = licensePlateObj?.ToString();
                    if (string.IsNullOrEmpty(licensePlate))
                    {
                        _logger.LogWarning($"Biển số không hợp lệ trong payload của sự kiện {item.time_stamp}");
                        continue;
                    }
                    string nameDriver;
                    if (!payload.TryGetValue("Name", out var nameDriverPayload))
                    {
                        _logger.LogWarning($"Không tìm thấy tên người cầm lái trong payload của sự kiện {item.time_stamp}");
                        continue;
                    }
                    nameDriver = nameDriverPayload.ToString();

                    var source = sources.FirstOrDefault(s => s.Guid == item.sourceID);
                    
                    // Tìm vehicle có biển số khớp với Lpr
                    var vehicle = vehicles.FirstOrDefault(v => v.Lpn == licensePlate);

                    // Bỏ qua nếu không tìm thấy vehicle có biển số khớp
                    if (vehicle == null)
                    {
                        _logger.LogInformation($"Bỏ qua sự kiện - Không tìm thấy vehicle cho biển số {licensePlate}");
                        continue;
                    }

                    // Tạo view model cho các sự kiện hợp lệ
                    viewModel.Add(new LprEventViewModel
                    {
                        Timestamp = item.time_stamp,
                        formatted_date = TimeStampHelper.ConvertTimestamp(item.time_stamp),
                        LicensePlate = licensePlate,
                        Owner = vehicle.Owner ?? "N/A",
                        DirverName = nameDriver,
                        CameraFr = source.Name,
                        Warning = "Không khớp"
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Lỗi xử lý sự kiện LPR tại timestamp {item.time_stamp}");
                    continue;
                }
            }

            _logger.LogInformation($"Đã xử lý {viewModel.Count} sự kiện LPR hợp lệ");
            return viewModel;
        }

        public async Task<FileContentResult> ExportExcelAsync(string fromDate, string toDate, string note, string locationId)
        {
            try
            {
                var (parsedFromDate, parsedToDate, fromTs, toTs) = _inOutService.ParseDateRange(fromDate, toDate);
                var data = await GetLprEventLogsAsync(fromTs, toTs, locationId, 1, int.MaxValue);
                var viewModel = await ProcessLprEventsAsync(data);

                var folder = Path.Combine(_env.WebRootPath, "ReportTemplate");
                var templatePath = Path.Combine(folder, "_TemplateLicensePlate.xlsx");
                var fileName = $"LicensePlate_Report_{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.xlsx";

                var stream = new MemoryStream();
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                using (var package = new ExcelPackage(new FileInfo(templatePath)))
                {
                    var worksheet = package.Workbook.Worksheets.FirstOrDefault() ?? throw new InvalidOperationException("No worksheet in template.");
                    worksheet.Cells["C2"].Value = $"{parsedFromDate:dd-MM-yyyy HH:mm} - {parsedToDate:dd-MM-yyyy HH:mm}";
                    worksheet.Cells["C3"].Value = "Biển số và mặt không khớp";
                    worksheet.Cells["C4"].Value = string.IsNullOrWhiteSpace(note) ? "Không có ghi chú" : note;

                    for (int i = 0; i < viewModel.Count; i++)
                    {
                        var row = i + 6;
                        worksheet.Cells[row, 1].Value = (i + 1).ToString();
                        worksheet.Cells[row, 2].Value = viewModel[i].formatted_date;
                        worksheet.Cells[row, 3].Value = viewModel[i].LicensePlate;
                        worksheet.Cells[row, 4].Value = viewModel[i].Owner;
                        worksheet.Cells[row, 5].Value = viewModel[i].DirverName;
                        worksheet.Cells[row, 6].Value = viewModel[i].CameraFr;
                        //worksheet.Cells[row, 7].Value = viewModel[i].Warning;
                    }

                    package.SaveAs(stream);
                }

                stream.Position = 0;
                return new FileContentResult(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
                {
                    FileDownloadName = fileName
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting Excel report");
                throw;
            }
        }
    }
}