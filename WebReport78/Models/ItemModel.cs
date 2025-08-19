namespace WebReport78.Models
{
    public class ItemModel
    {
        public string Name { get; set; }
        public string CheckTime { get; set; }
        public string CheckType { get; set; }
        public string Source { get; set; }
        public string EndTime { get; set; }

        public string IdCard { get; set; }    // Dùng cho CurrentGuests và soliders
        public string Gender { get; set; }

        public string Phone_number { get; set; }

        public string document_number { get; set; }
    }
}
