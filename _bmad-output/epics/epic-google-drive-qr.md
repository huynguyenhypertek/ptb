# Epic: Tích hợp Google Drive — Lưu ảnh & QR chia sẻ

## Mục tiêu

Thay thế hệ thống upload ảnh qua API server + ngrok bằng Google Drive:
- Ảnh chụp lưu trực tiếp vào thư mục Google Drive Desktop (tự sync lên cloud)
- Tạo link chia sẻ tự động qua Google Apps Script (miễn phí, không cần Cloud Console)
- Khách scan QR → mở Google Drive folder → tải tất cả ảnh

## Tổng quan Stories

| Story | Tên | Mức độ | Trạng thái |
|---|---|---|---|
| 1 | Cấu hình đường dẫn Google Drive | 🟡 Nhẹ | `done` |
| 2 | Google Apps Script Web App | 🟠 Trung bình | `done` |
| 3 | Tích hợp QR từ Google Drive link | 🟠 Trung bình | `ready-for-dev` |

## Thứ tự thực hiện

```
Story 1 (Config) ──→ Story 2 (Apps Script) ──→ Story 3 (QR Integration)
```

- **Story 1 & 2** có thể làm song song (1 sửa code, 2 setup trên Google Drive)
- **Story 3** phụ thuộc cả Story 1 và Story 2

## Điều kiện tiên quyết

- [x] Google Drive Desktop đã cài trên máy
- [ ] Chọn tài khoản Google Drive để dùng
- [ ] Tạo folder `PhotoBooth` trên Google Drive và share public

## Definition of Done

- [ ] Ảnh chụp lưu vào thư mục Google Drive trên máy tính
- [ ] Ảnh tự động sync lên Google Drive cloud
- [ ] QR code chứa link Google Drive folder
- [ ] Khách scan QR → thấy tất cả ảnh trong session (ảnh gốc + ảnh ghép)
- [ ] App vẫn hoạt động bình thường khi không có internet (lưu ảnh local, bỏ qua QR)
- [ ] Build thành công, không lỗi
