using MongoDB.Driver;
using System.Collections.Generic;
using WebReport78.Models;

namespace WebReport78.Services
{
    // Interface cho việc đọc/ghi file JSON (settingcamera.json, currentsoldiers.json, manualactions.json)
    public interface IJsonFileService
    {
        CameraSettings LoadCameraSettings();
        string GetLocationId();
        List<CurrentSoldier> LoadCurrentSoldiers();
        void SaveCurrentSoldiers(List<CurrentSoldier> soldiers);
        List<ManualAction> LoadManualActions();
        void SaveManualActions(List<ManualAction> actions);
        IMongoCollection<T> GetMongoCollection<T>(string collectionName); // Để LprService truy cập MongoDB nếu cần
    }
}