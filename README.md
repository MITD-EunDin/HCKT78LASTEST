# HCKT78LASTEST
```
├── wwwroot
├──── css                                 // viết css
├──── js                                  // viết js
├──── lib                                 // thư viện bootrap có css,js sẵn (dùng để fontend)
├── Controllers
├──── InOutController.cs                  // báo cáo vào ra
├──── ProtectDutyController.cs            // trực ban
├──── LprReportController.cs              // cảnh báo biển số
├── Models
├──── DutyShift.cs                        // tạo 1 table mới trong SQL Xguard để quản lý trực ban
├──── eventLog.cs
├──── ErrorViewModel.cs
├──── ItemModel.cs                         // model lưu trữ dữ liệu vào ra temp để xuất excel
├──── LprEventViewModel.cs                 // lưu dữ liệu cảnh bảo biển số
├──── MongoDbService.cs                    // connect tới mongodb
├──── MongoDbSettings
├──── Source.cs                           // thông tin camera trong xguard
├──── Staff.cs
├──── StaffIdentity.cs
├──── Vehicle.cs
├──── XGuardContext.cs                    // connect tới xguard
├── Views
├──── InOut
├─────── Header.cshtml
├─────── Index.cshtml
├──── ProtectDuty
├─────── Create.cshtml
├─────── Edit.cshtml
├─────── HeaderSection.cshtml
├─────── ImportFile.cshtml
├─────── Index.cshtml
├─────── Sidebar.cshtml
├──── lprReport
├─────── Index.cshtml
├──── Shared
├─────── _Layout.cshtml                  // giao diện tổng
├─────── _toastNotice.cshtml             // giaao diện thông báo thành công và lỗi
├─────── _ExportReport.cshtml
├── appsettings.json
├── efpt.config.json
└──Program.cs 
```

# CÁC PHẦN LƯU Ý

- Sửa connectstring và database name trong appsetting.json phần MongoDb (ĐÃ SỬA ĐÚNG THEO H2Xsmart)
- Sửa DbWeSmart.cs trong phần Models sao cho đúng tên Collection của Eventlog (ĐÃ SỬA)
- (LƯU Ý)Trong Controllers -> InOutController.cs, LprReportController.cs hàm ExReport phần [var folder = @"D:\excel"] SỬA CHO ĐÚNG ĐƯỜNG ĐÃN file Template Excel mẫu

# InOut (Models: ItemModel(lưu dữ liệu ra view), eventLog, source, staff)

- báo cáo vào ra, quân số hiện tại, tổng quân số, số khách
- xuất báo cáo theo ngày (dự liệu quân số hiện tại, tổng quân số, số khách hiện theo today)

# ProtectDuty (Models: DutyShif)

- Nhập file xlsx và bấm lưu để import thanh công
- cho phép nhập tay và sửa xóa
- có thể xem được những trực ban của tháng trước nhưng không sửa xóa thêm được

# LprReport (Models: LprEventViewModel(lưu dữ liệu ra view), eventLog, MongoDbService, MongoDbSettings, StaffIdentity, Vehicle ,eventLog)

- hiện những sự kiện biển số và cảnh báo biển mặt không khớp
- có thể xuất báo cáo biển số
- cảnh báo biển số không khớp: lấy sự kiện trước event_type = 101 


# logic trực báo cáo vào ra (đã thêm chức năng nhập xóa quân số hiện tại[chỉ cần nhập document_number press enter là các trường khác tự động điền])

- Tổng quân số đêm tất cả người trong bảng staff
- Quân số hiện tại tính theo camera, check in ++, check out -- ;
- Số Khách tính theo datecreate == today và idTypePerson == 3 trong Xguard
- Cảnh báo đi muộn về sớm tính theo id_type_person : == 0 (các sếp) không tính, == 2 gán đi muộn về sớm
  - đi muộn: sau 8h30 gán đi muộn
  - về sớm: trước 17h30 gán về sớm nhưng trước 17h30 checkin thì bỏ gán về sớm
