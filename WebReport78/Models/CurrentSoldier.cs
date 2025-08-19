namespace WebReport78.Models
{
    public class CurrentSoldier
    {
        public string UserGuid_cur { get; set; }
        public string Name_cur { get; set; }
        public string IdCard_cur { get; set; }
        public string Gender_cur { get; set; }
        public string PhoneNumber_cur { get; set; }
    }
    // dùng này để lưu vào json
    public class CurrentSoldiersData
    {
        public List<CurrentSoldier> Soldiers { get; set; } = new List<CurrentSoldier>();
        public long LastProcessedTimestamp { get; set; } = 0;  // Default 0 nếu chưa process
    }
}
