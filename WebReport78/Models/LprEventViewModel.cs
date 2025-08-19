namespace WebReport78.Models
{
    public class LprEventViewModel
    {
        public int Timestamp { get; set; } // Thời gian sự kiện (formatted_date)
        public string formatted_date { get; set; } // lưu timestampe
        public string LicensePlate { get; set; } // Biển số
        public string Owner { get; set; } // Tên người sở hữu (từ FR hoặc Staff)
        public string CameraLpr { get; set; } // camera biển
        public string DirverName { get; set; }
        public string CameraFr { get; set; } // camera mặt
        public string Warning { get; set; } // Cảnh báo (nếu không khớp)
    }
}
