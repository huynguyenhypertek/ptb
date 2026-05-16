# Epic: Offline Fallback — Mã số ảnh & Xử lý mất mạng

## Mục tiêu

Đảm bảo hệ thống photobooth **không bị gián đoạn khi mất mạng** bằng cách:
- Gán **mã số thứ tự** cho mỗi session (dạng `0516-001`)
- **In mã số** nhỏ lên góc ảnh cuối cùng để khách nhận diện
- Khi offline: **hiện thông báo liên hệ nhân viên** + mã số thay vì QR code
- Ảnh vẫn lưu vào Google Drive local → **tự sync khi có mạng**

## Bối cảnh

- Khi mất mạng, không thể gọi Google Apps Script để lấy Google Drive URL → không tạo được QR
- Google Drive Desktop vẫn lưu ảnh local và **tự sync khi mạng khôi phục**
- Khi offline, chỉ cần hiện thông báo "Hãy liên hệ nhân viên hỗ trợ" — chủ booth sẽ trao đổi trực tiếp với khách

## Tổng quan Stories

| Story | Tên | Mức độ | Trạng thái |
|---|---|---|---|
| 1 | Sequential Number Service — Quản lý mã số thứ tự | 🟡 Nhẹ | `ready-for-dev` |
| 2 | In mã số lên ảnh cuối (Compositing) | 🟠 Trung bình | `ready-for-dev` |
| 3 | Detect offline & Hiện thông báo liên hệ nhân viên | 🟡 Nhẹ | `done` |

## Thứ tự thực hiện

```
Story 1 (Mã số) ──→ Story 2 (In lên ảnh)
                ──→ Story 3 (Offline UI)
```

- **Story 1** phải làm trước (cung cấp mã số cho Story 2 & 3)
- **Story 2 & 3** có thể làm song song sau Story 1

## Điều kiện tiên quyết

- [x] Google Drive Desktop đã cài trên máy
- [x] Epic Google Drive QR đã hoàn thành (Story 1 & 2 done)
- [x] `ImageCompositeService` đã hoạt động (OpenCV compositing)

## Definition of Done

- [ ] Mỗi session có mã số thứ tự duy nhất (dạng `MMDD-NNN`)
- [ ] Mã số được in nhỏ ở góc ảnh cuối (không ảnh hưởng thẩm mỹ)
- [ ] Khi mất mạng: UI hiện "Hãy liên hệ nhân viên hỗ trợ" + mã số ảnh
- [ ] Khi có mạng: QR hoạt động bình thường (không thay đổi)
- [ ] Counter persistent — restart app không reset về 0
- [ ] Counter auto-reset mỗi ngày (tránh số quá lớn)
- [ ] Build thành công, không lỗi
