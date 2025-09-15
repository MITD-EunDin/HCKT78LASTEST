using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using WebReport78.Models;

namespace WebReport78.Services
{
    public class JsonFileService : IJsonFileService
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<JsonFileService> _logger;
        private readonly MongoDbService _mongoService;

        public JsonFileService(IWebHostEnvironment env, ILogger<JsonFileService> logger, MongoDbService mongoService)
        {
            _env = env;
            _logger = logger;
            _mongoService = mongoService;
        }

        public CameraSettings LoadCameraSettings()
        {
            try
            {
                var path = Path.Combine(_env.ContentRootPath, "appsettings.json");
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<CameraSettings>(json) ?? throw new InvalidOperationException("Invalid camera settings JSON.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading camera settings");
                throw;
            }
        }

        public string GetLocationId()
        {
            return LoadCameraSettings().location_id ?? throw new InvalidOperationException("Location ID not found.");
        }

        public List<CurrentSoldier> LoadCurrentSoldiers()
        {
            try
            {
                var path = Path.Combine(_env.ContentRootPath, "currentsoldiers.json");
                if (!File.Exists(path)) File.WriteAllText(path, "[]");
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<List<CurrentSoldier>>(json) ?? new List<CurrentSoldier>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading current soldiers");
                throw;
            }
        }

        public void SaveCurrentSoldiers(List<CurrentSoldier> soldiers)
        {
            try
            {
                var path = Path.Combine(_env.ContentRootPath, "currentsoldiers.json");
                var json = JsonSerializer.Serialize(soldiers, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving current soldiers");
                throw;
            }
        }

        public List<ManualAction> LoadManualActions()
        {
            try
            {
                var path = Path.Combine(_env.ContentRootPath, "manualactions.json");
                if (!File.Exists(path)) File.WriteAllText(path, "[]");
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<List<ManualAction>>(json) ?? new List<ManualAction>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading manual actions");
                throw;
            }
        }

        public void SaveManualActions(List<ManualAction> actions)
        {
            try
            {
                var path = Path.Combine(_env.ContentRootPath, "manualactions.json");
                var json = JsonSerializer.Serialize(actions, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving manual actions");
                throw;
            }
        }

        public IMongoCollection<T> GetMongoCollection<T>(string collectionName)
        {
            return _mongoService.GetCollection<T>(collectionName);
        }
    }
}