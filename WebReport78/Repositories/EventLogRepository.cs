using MongoDB.Driver;
using System.Collections.Generic;
using System.Threading.Tasks;
using WebReport78.Models;
using WebReport78.Services;

namespace WebReport78.Repositories
{
    public class EventLogRepository : IEventLogRepository
    {
        private readonly MongoDbService _mongoService;

        public EventLogRepository(MongoDbService mongoService)
        {
            _mongoService = mongoService;
        }

        // Xây dựng filter cho truy vấn MongoDB
        private FilterDefinition<eventLog> BuildFilter(long fromTs, long toTs, string locationId, int? typeEvent)
        {
            var filters = new List<FilterDefinition<eventLog>>
            {
                Builders<eventLog>.Filter.Gte(x => x.time_stamp, fromTs),
                Builders<eventLog>.Filter.Lte(x => x.time_stamp, toTs),
                Builders<eventLog>.Filter.Eq(x => x.locationId, locationId)
            };

            if (typeEvent.HasValue)
                filters.Add(Builders<eventLog>.Filter.Eq(x => x.typeEvent, typeEvent.Value));
            else
                filters.Add(Builders<eventLog>.Filter.In(x => x.typeEvent, new[] { 1, 25 }));

            return Builders<eventLog>.Filter.And(filters);
        }

        public async Task<long> GetTotalRecordsAsync(long fromTs, long toTs, string locationId, int? typeEvent = null)
        {
            var collection = _mongoService.GetCollection<eventLog>("EventLog");
            return await collection.CountDocumentsAsync(BuildFilter(fromTs, toTs, locationId, typeEvent));
        }

        public async Task<List<eventLog>> GetEventLogsAsync(long fromTs, long toTs, string locationId, int page, int pageSize, int? typeEvent = null)
        {
            var collection = _mongoService.GetCollection<eventLog>("EventLog");
            return await collection.Find(BuildFilter(fromTs, toTs, locationId, typeEvent))
                .SortByDescending(x => x.time_stamp) // giảm dần
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();
        }
    }
}