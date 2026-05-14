# Epic: Sửa Lỗi & Cải thiện Độ ổn định Hệ thống PhotoBooth

## Mục tiêu

Khắc phục 21 lỗi & rủi ro được phát hiện từ quá trình rà soát toàn bộ dự án, đảm bảo hệ thống PhotoBooth có thể chạy ổn định 24/7 trong môi trường production (máy Kiosk đặt tại cửa hàng).

## Tổng quan Stories

| Story | Tên | Mức độ | Lỗi liên quan | Trạng thái |
|---|---|---|---|---|
| 1 | Bảo mật API Server | 🔴 Nghiêm trọng | Lỗi 1, 2, 7 | `draft` |
| 2 | Rò rỉ Bitmap (Memory Leak) | 🔴 Nghiêm trọng | Lỗi 3, 4, 5, 8, 9 | `done` |
| 3 | Ổn định Camera (Long-running) | 🔴 Nghiêm trọng | Lỗi 6, 10 | `draft` |
| 4 | Tập trung hoá Network (URL + HttpClient) | 🟠 Trung bình | Lỗi 11, 13 | `draft` |
| 5 | Quản lý Ổ đĩa (Session Cleanup) | 🟠 Trung bình | Lỗi 14 | `draft` |
| 6 | Code Quality & Cleanup | 🟡 Nhẹ | Lỗi 12, 15, 16, 17, 18, 19, 20, 21 | `draft` |

## Thứ tự thực hiện

```
Story 1 (Bảo mật) ──→ Story 4 (Network) ──→ Story 6 (Cleanup)
                                ↑
Story 2 (Bitmap) ──→ Story 3 (Camera) ──→ Story 5 (Disk)
```

- **Story 1 & 2** có thể làm song song (không phụ thuộc nhau)
- **Story 3** phụ thuộc Story 2 (cùng sửa CaptureViewModel)
- **Story 4** nên làm sau Story 1 (cùng liên quan API)
- **Story 5 & 6** làm cuối cùng

## Definition of Done

- [ ] Tất cả lỗi được sửa theo từng story
- [ ] Ứng dụng build thành công không lỗi
- [ ] Chạy thử ít nhất 10 session liên tiếp không crash, không tăng RAM bất thường
- [ ] API endpoints nhạy cảm yêu cầu token JWT hợp lệ
