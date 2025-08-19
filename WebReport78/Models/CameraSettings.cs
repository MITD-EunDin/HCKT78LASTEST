namespace WebReport78.Models
{
    public class CameraSetting
    {
        public string source_id { get; set; }
        public string name { get; set; }
        public string type { get; set; }
    }
    public class CameraSettings
    {
        public string location_id { get; set; }
        public List<CameraSetting> cameras { get; set; }
        public List<CameraSetting> camerasLprPair { get; set; }
    }
}
