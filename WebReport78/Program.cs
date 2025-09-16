using Microsoft.EntityFrameworkCore;
using System.Globalization;
using WebReport78.Models;
using OfficeOpenXml;
using WebReport78.Config;
using WebReport78.Services;
using MongoDB.Driver.Core.Configuration;
using WebReport78.Repositories;
using WebReport78.Model2s;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();


// Đăng ký XGuardContext với chuỗi kết nối
//builder.Services.AddDbContext<XGuardContext>(options =>
//    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// THÊM MỚI: Đăng ký DbContextFactory để sử dụng trong InOutController
builder.Services.AddDbContextFactory<XGuardContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Db1")));

builder.Services.AddDbContextFactory<H2XSmartContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Db2")));

// Kết nối MongoDB
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDbSettings"));

builder.Services.AddSingleton(sp =>
{
    var settings = sp.GetRequiredService<
        Microsoft.Extensions.Options.IOptions<MongoDbSettings>>().Value;
    return new MongoDbService(settings);
});

// Repositories
builder.Services.AddScoped<IStaffRepository, StaffRepository>();
builder.Services.AddScoped<IEventLogRepository, EventLogRepository>();
// model2s
builder.Services.AddScoped<IGatewayMemberRepository, GatewayMemberRepository>();
builder.Services.AddScoped<ISsoUserRepository, SsoUserRepository>();

// Services
builder.Services.AddScoped<IInOutService, InOutService>();
builder.Services.AddScoped<ILprService, LprService>();
builder.Services.AddScoped<IReportService, ReportService>(); 
builder.Services.AddSingleton<IJsonFileService, JsonFileService>();
//login
builder.Services.AddScoped<IAuthService, AuthService>();

//builder.Services.AddScoped<IFirstInCheckoutService, FirstInCheckoutService>();

// Session để lưu vai trò sau khi đăng nhập
builder.Services.AddHttpContextAccessor();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Thời gian hết hạn session
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Cache
builder.Services.AddMemoryCache();

// Set license cho EPPlus libary
ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}
app.UseStaticFiles();

app.UseSession();

app.UseRouting();

app.UseAuthorization();

//app.MapControllerRoute(
//    name: "default",
//    pattern: "{controller=ProtectDuty}/{action=Index}/{id?}");


// Điều hướng trang mặc định đến trang đăng nhập
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Index}/{id?}");
// điều hướng trang

//app.MapControllerRoute(
//    name: "ProtectDuty",
//    pattern: "{controller=ProtectDuty}/{action=Index}/{id?}");

app.Run();
