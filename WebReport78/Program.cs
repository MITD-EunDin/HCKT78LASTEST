using Microsoft.EntityFrameworkCore;
using System.Globalization;
using WebReport78.Models;
using OfficeOpenXml;
using WebReport78.Config;
using WebReport78.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Đăng ký XGuardContext với chuỗi kết nối
builder.Services.AddDbContext<XGuardContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
// Kết nối MongoDB
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDbSettings"));
builder.Services.AddSingleton(sp =>
{
    var settings = sp.GetRequiredService<
        Microsoft.Extensions.Options.IOptions<MongoDbSettings>>().Value;
    return new MongoDbService(settings);
});

// Set license cho EPPlus libary
ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

//app.MapControllerRoute(
//    name: "default",
//    pattern: "{controller=ProtectDuty}/{action=Index}/{id?}");

// điều hướng trang
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=ProtectDuty}/{action=Index}/{id?}");

app.Run();
