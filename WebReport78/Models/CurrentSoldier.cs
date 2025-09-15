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

    public class ManualAction
    {
        public string UserGuid { get; set; }
        public long? LastActionTimestamp { get; set; }
        public int LastActionType { get; set; } // "add==2" or "remove==1"
    }
}
