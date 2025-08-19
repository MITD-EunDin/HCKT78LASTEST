using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
namespace WebReport78.Models
{
    [BsonIgnoreExtraElements]
    public class eventLog
    {
        //[BsonId] // MongoDB tự quản lý _id
        //[BsonRepresentation(BsonType.ObjectId)]
        //public string Id { get; set; }

        [BsonElement("event_name")] 
        public string Name { get; set; }

        [BsonElement("event_type")] 
        public int typeEvent { get; set; }

        [BsonElement("guid")]
        public string userGuid { get; set; }

        [BsonElement("location_id")]
        public string locationId { get; set; }

        [BsonElement("processor_id")]
        public string ProcessorId { get; set; }

        [BsonElement("source_id")] 
        public string sourceID { get; set; }

        [BsonElement("timestamp")]
        public int time_stamp { get; set; }
        public string formatted_date { get; set; } // format giá trị timestamp -> dd-mm-uy
        public string type_eventLE { get; set; } // gán giá trị đi muộn về sớm
        public string type_eventIO { get; set; } // lưu loại vào ra
        public string cameraGuid { get; set; } // lưu GUID gốc từ MongoDB
        public string cameraName { get; set; } // lưu tên hiển thị từ SQL

        // đánh dấu vào ra
        public bool IsLate { get; set; }
        public bool IsLeaveEarly { get; set; }

        public string Lpr { get; set; } // biển số hoặc sự kiện biển số mặt không khớp
        public bool IsPushed { get; set; } // check biển mặt số khớp hay không khớp

        // dữ liệu của bảng số khách
        public string idCard { get; set; }
        public string Gender { get; set; }
        // dữ liệu bảng quân số hiện tại
        public string phone { get; set; }
    }
}
