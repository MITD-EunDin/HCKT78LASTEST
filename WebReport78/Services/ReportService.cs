using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using WebReport78.Interfaces;
using WebReport78.Models;
using WebReport78.Repositories;

namespace WebReport78.Services
{
    public class ReportService : IReportService
    {
        private readonly IInOutService _inOutService;
        private readonly IStaffRepository _staffRepo;
        private readonly IEventLogRepository _eventLogRepo;
        private readonly IJsonFileService _jsonService;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<ReportService> _logger;

        public ReportService(IInOutService inOutService, IStaffRepository staffRepo, IEventLogRepository eventLogRepo, IJsonFileService jsonService, IWebHostEnvironment env, ILogger<ReportService> logger)
        {
            _inOutService = inOutService;
            _staffRepo = staffRepo;
            _eventLogRepo = eventLogRepo;
            _jsonService = jsonService;
            _env = env;
            _logger = logger;
        }

        public async Task<FileContentResult> ExportInOutReportAsync(string fromDate, string toDate, string note, string filterType, string locationId)
        {
            try
            {
                var (parsedFromDate, parsedToDate, fromTs, toTs) = _inOutService.ParseDateRange(fromDate, toDate);
                var (soldierTotal, soldierCurrent, guestCount, guestCurrent) = await _inOutService.GetSummaryAsync(fromTs, toTs, locationId, parsedFromDate);

                List<eventLog> data;
                if (filterType == "CurrentGuests")
                {
                    var guests = await _inOutService.GetCurrentGuestsAsync(parsedFromDate, parsedToDate);
                    data = guests.Select(g => new eventLog
                    {
                        Name = g.Name,
                        formatted_date = g.StartTime.HasValue ? TimeStampHelper.ConvertTimestamp(g.StartTime.Value) : "N/A",
                        idCard = g.DocumentNumber,
                        Gender = g.Gender == 1 ? "Nam" : "Nữ"
                    }).ToList();
                }
                else
                {
                    var validSources = (await _staffRepo.GetSourcesAsync()).Where(s => s.AcCheckType == 1 || s.AcCheckType == 2).Select(s => s.Guid).ToList();
                    data = await _inOutService.GetFilteredDataAsync(filterType, fromTs, toTs, locationId, parsedFromDate, parsedToDate, validSources);
                    data = data.Where(x => !string.IsNullOrEmpty(x.Name) && x.Name != "Unknown").ToList();
                }

                var itemList = filterType switch
                {
                    "CurrentGuests" => data.Select(item => new ItemModel
                    {
                        Name = item.Name,
                        CheckTime = item.formatted_date ?? "N/A",
                        IdCard = item.idCard ?? "N/A",
                        Gender = item.Gender ?? "N/A"
                    }).ToList(),
                    "CurrentSoldiers" => data.Select(item => new ItemModel
                    {
                        Name = item.Name,
                        IdCard = item.idCard ?? "N/A",
                        Gender = item.Gender ?? "N/A",
                        Phone_number = item.phone ?? "N/A"
                    }).ToList(),
                    _ => data.Select(item => new ItemModel
                    {
                        Name = item.Name,
                        CheckTime = item.formatted_date,
                        CheckType = item.type_eventIO,
                        Source = item.sourceID,
                        EndTime = item.type_eventLE,
                        OutTime = item.count_duration
                    }).ToList()
                };

                var folder = Path.Combine(_env.WebRootPath, "ReportTemplate");
                string templatePath = filterType switch
                {
                    "CurrentGuests" => Path.Combine(folder, "_TemplateGuests.xlsx"),
                    "CurrentSoldiers" => Path.Combine(folder, "_TemplateSoldiers.xlsx"),
                    "All" => Path.Combine(folder, "_TemplateWeb78All.xlsx"),
                    _ => Path.Combine(folder, "_TemplateWeb78.xlsx")
                };
                var fileName = filterType switch
                {
                    "CurrentGuests" => $"CurrentGuests_Report_{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.xlsx",
                    "CurrentSoldiers" => $"CurrentSoldiers_Report_{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.xlsx",
                    _ => $"Attendance_Report_{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.xlsx"
                };

                var stream = new MemoryStream();
                ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
                using (var package = new ExcelPackage(new FileInfo(templatePath)))
                {
                    var worksheet = package.Workbook.Worksheets.FirstOrDefault() ?? throw new InvalidOperationException("Không tìm thấy worksheet trong template.");
                    worksheet.Cells["C2"].Value = $"{parsedFromDate:dd-MM-yyyy HH:mm} - {parsedToDate:dd-MM-yyyy HH:mm}";
                    worksheet.Cells["C3"].Value = $"{soldierCurrent} / {soldierTotal}";
                    worksheet.Cells["E3"].Value = $"{guestCount}";
                    if (filterType == "All")
                    {
                        worksheet.Cells["C4"].Value = "Tất Cả";
                    } 
                    else if ( filterType == "Late")
                    {
                        worksheet.Cells["C4"].Value = "Đi Muộn";
                    }
                    else if ( filterType == "Early")
                    {
                        worksheet.Cells["C4"].Value = "Về Sớm";
                    }
                    else if (filterType == "CurrentSoldiers")
                    {
                        worksheet.Cells["C4"].Value = "Quân số hiện tại";
                    }
                    else
                    {
                        worksheet.Cells["C4"].Value = "Số lượng khách";
                    }

                    worksheet.Cells["C5"].Value = string.IsNullOrWhiteSpace(note) ? "Không có ghi chú" : note;

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
                            if (filterType == "All")
                                worksheet.Cells[row, 6].Value = itemList[i].OutTime;
                        }
                    }
                    package.SaveAs(stream);
                }

                stream.Position = 0;
                return new FileContentResult(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet") { FileDownloadName = fileName };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xuất báo cáo InOut");
                throw;
            }
        }
    }
}