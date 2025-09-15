using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WebReport78.Models;

namespace WebReport78.Repositories
{
    public class StaffRepository : IStaffRepository
    {
        private readonly IDbContextFactory<XGuardContext> _contextFactory;
        private readonly IMemoryCache _cache;

        public StaffRepository(IDbContextFactory<XGuardContext> contextFactory, IMemoryCache cache)
        {
            _contextFactory = contextFactory;
            _cache = cache;
        }

        // danh sách quân số hiện tại
        public async Task<int> GetSoldierTotalAsync()
        {
            using var context = _contextFactory.CreateDbContext();
            return await context.Staff
                .CountAsync(s => s.IdTypePerson.HasValue && (s.IdTypePerson.Value == 0 || s.IdTypePerson.Value == 2));
        }

        // danh sách khách hiện tại
        public async Task<List<Staff>> GetGuestsAsync(long fromTs, long toTs)
        {
            using var context = _contextFactory.CreateDbContext();
            return await context.Staff
                .Where(s => s.IdTypePerson == 3 && s.StartTime >= fromTs && s.StartTime <= toTs)
                .ToListAsync();
        }

        // danh sách khách theo tìm kiếm ngày
        public Task<int> GetGuestCurrentTodayAsync(List<Staff> guests, long toTs)
        {
            return Task.FromResult(guests.Count(s => !s.EndTime.HasValue || s.EndTime > toTs));
        }

        // lấy thông tin nhân viên
        public async Task<List<Staff>> GetStaffListAsync()
        {
            const string cacheKey = "StaffList";
            if (!_cache.TryGetValue(cacheKey, out List<Staff> staffList))
            {
                using var context = _contextFactory.CreateDbContext();
                staffList = await context.Staff
                    .Where(s => s.IdTypePerson.HasValue && (s.IdTypePerson.Value == 0 || s.IdTypePerson.Value == 2))
                    .ToListAsync();
                _cache.Set(cacheKey, staffList, TimeSpan.FromHours(1));
            }
            return staffList;
        }

        public async Task<List<Vehicle>> GetVehiclesAsync()
        {
            const string cacheKey = "Vehicles";
            if (!_cache.TryGetValue(cacheKey, out List<Vehicle> vehicles))
            {
                using var context = _contextFactory.CreateDbContext();
                vehicles = await context.Vehicles.ToListAsync();
                _cache.Set(cacheKey, vehicles, TimeSpan.FromHours(1));
            }
            return vehicles;
        }

        public async Task<List<Source>> GetSourcesAsync()
        {
            const string cacheKey = "Sources";
            if (!_cache.TryGetValue(cacheKey, out List<Source> sources))
            {
                using var context = _contextFactory.CreateDbContext();
                sources = await context.Sources
                    .Select(s => new Source
                    {
                        Guid = s.Guid,
                        Name = s.Name,
                        AcCheckType = s.AcCheckType
                    })
                    .ToListAsync();
                _cache.Set(cacheKey, sources, TimeSpan.FromHours(1));
            }
            return sources;
        }

        // tìm nhân viên theo cccd (để thêm quân số hiện tại theo cccd)
        public async Task<Staff> GetStaffByDocumentNumberAsync(string idCard)
        {
            using var context = _contextFactory.CreateDbContext();
            return await context.Staff
                .FirstOrDefaultAsync(s => s.DocumentNumber == idCard && s.IdTypePerson.HasValue && (s.IdTypePerson.Value == 0 || s.IdTypePerson.Value == 2));
        }
    }
}