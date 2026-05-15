# 📘 Hướng dẫn cấu hình PhotoBooth — Google Drive & Apps Script

> Tài liệu này giúp bạn tự cấu hình **từ đầu đến cuối**, không cần kiến thức lập trình.

---

## Mục lục

1. [Tổng quan: Hệ thống hoạt động ra sao?](#1-tổng-quan-hệ-thống-hoạt-động-ra-sao)
2. [Kịch bản A: Lưu ảnh trên ổ cứng (Local) — QR qua Ngrok](#2-kịch-bản-a-lưu-ảnh-trên-ổ-cứng-local--qr-qua-ngrok)
3. [Kịch bản B: Lưu ảnh trên Google Drive — QR qua Google Drive](#3-kịch-bản-b-lưu-ảnh-trên-google-drive--qr-qua-google-drive)
4. [Cấu hình Google Apps Script (chi tiết từng bước)](#4-cấu-hình-google-apps-script-chi-tiết-từng-bước)
5. [Lệnh chạy app — Copy & Paste](#5-lệnh-chạy-app--copy--paste)
6. [**Bản đồ file code — Vị trí lưu ảnh được điều khiển ở đâu?**](#6-bản-đồ-file-code--vị-trí-lưu-ảnh-được-điều-khiển-ở-đâu)
7. [Xử lý sự cố (Troubleshooting)](#7-xử-lý-sự-cố-troubleshooting)

---

## 1. Tổng quan: Hệ thống hoạt động ra sao?

```
┌─────────────────────────────────────────────────────────────────────┐
│                        PhotoBooth App                               │
│                                                                     │
│  Chụp ảnh → Lưu file ảnh → Tạo QR code → Khách scan QR → Tải ảnh  │
└─────────────────────────────────────────────────────────────────────┘
```

Có **2 câu hỏi** bạn cần trả lời:

| Câu hỏi | Lựa chọn A (Local) | Lựa chọn B (Google Drive) |
|----------|-------------------|--------------------------|
| **Ảnh lưu ở đâu?** | `~/Pictures/PhotoBooth/` (ổ cứng) | Thư mục Google Drive trên máy (tự sync lên cloud) |
| **QR dẫn đến đâu?** | Link Ngrok (cần chạy server ngrok) | Link Google Drive (miễn phí, không cần server) |

### Sơ đồ luồng hoạt động

```
KỊCH BẢN A (Local + Ngrok):
  Chụp ảnh → Lưu vào ~/Pictures/PhotoBooth/ → Upload ảnh lên ngrok server → QR = link ngrok

KỊCH BẢN B (Google Drive):
  Chụp ảnh → Lưu vào thư mục Google Drive trên máy
           → Google Drive Desktop TỰ ĐỘNG sync lên cloud
           → App gọi Apps Script để share folder
           → QR = link Google Drive folder
```

---

## 2. Kịch bản A: Lưu ảnh trên ổ cứng (Local) — QR qua Ngrok

### Đây là chế độ MẶC ĐỊNH — không cần cấu hình gì thêm.

**Cách hoạt động:**
1. App lưu ảnh vào `~/Pictures/PhotoBooth/{session_id}/`
2. App upload ảnh lên server Ngrok
3. QR code chứa link Ngrok để khách tải ảnh

**Lệnh chạy:**
```bash
dotnet run --project src/PhotoBooth.UI -- \
  --storeId=1 \
  --deviceId=device-1 \
  --apiBaseUrl="https://your-ngrok-url.ngrok-free.dev"
```

**Yêu cầu:**
- ✅ Không cần Google Drive Desktop
- ✅ Không cần Apps Script
- ❗ CẦN chạy Ngrok server
- ❗ CẦN có internet (để upload ảnh)

**QR code sẽ chứa:** `https://your-ngrok-url.ngrok-free.dev/photos/...`

---

## 3. Kịch bản B: Lưu ảnh trên Google Drive — QR qua Google Drive

### Đây là chế độ cần cấu hình thêm — nhưng MIỄN PHÍ và không cần Ngrok.

**Cách hoạt động:**

```
Bước 1: App lưu ảnh vào thư mục Google Drive trên máy tính
        ↓
Bước 2: Google Drive Desktop (app của Google) TỰ ĐỘNG sync file lên Google Drive cloud
        ↓
Bước 3: App gọi Google Apps Script (web API miễn phí) để:
        - Tìm folder session trên Drive
        - Set quyền "Anyone with link can view"
        - Trả về link folder
        ↓
Bước 4: App tạo QR code từ link Google Drive
        ↓
Bước 5: Khách scan QR → mở Google Drive folder → tải ảnh
```

### Bạn cần chuẩn bị 3 thứ:

| # | Cần chuẩn bị | Mục đích | Hướng dẫn |
|---|-------------|---------|-----------|
| 1 | **Google Drive Desktop** | Sync file từ máy lên cloud tự động | [Bước 3.1](#31-cài-google-drive-desktop) |
| 2 | **Tìm đường dẫn thư mục Google Drive** | Biết path để truyền vào app | [Bước 3.2](#32-tìm-đường-dẫn-google-drive-trên-máy) |
| 3 | **Google Apps Script** | Web API share folder + trả link | [Phần 4](#4-cấu-hình-google-apps-script-chi-tiết-từng-bước) |

---

### 3.1. Cài Google Drive Desktop

1. Tải Google Drive Desktop: https://www.google.com/drive/download/
2. Cài đặt và đăng nhập bằng tài khoản Google của bạn
3. Chờ app sync xong (biểu tượng Google Drive trên taskbar/menu bar không còn quay)

### 3.2. Tìm đường dẫn Google Drive trên máy

Đường dẫn Google Drive **khác nhau** tuỳ hệ điều hành:

**🍎 macOS:**
```
/Users/<tên_user>/Library/CloudStorage/GoogleDrive-<email>/My Drive
```

Ví dụ thực tế:
```
/Users/huynguyen/Library/CloudStorage/GoogleDrive-dhgaming12th4@gmail.com/My Drive
```

> 💡 **Mẹo tìm nhanh trên Mac:**
> 1. Mở Finder
> 2. Nhìn sidebar trái → thấy "Google Drive" hoặc "My Drive"
> 3. Click chuột phải vào "My Drive" → **Get Info**
> 4. Copy đường dẫn ở mục "Where"

**🪟 Windows:**
```
G:\My Drive
```
(hoặc ổ đĩa khác tuỳ cài đặt — kiểm tra trong Google Drive app → Preferences → Google Drive streaming location)

**🐧 Linux:**
Google Drive Desktop không có cho Linux. Dùng kịch bản A (Local + Ngrok).

### 3.3. Hiểu cấu trúc folder — QUAN TRỌNG ⭐

Đây là phần nhiều người bị rối nhất. Hãy đọc kỹ.

#### Nguyên tắc cốt lõi

```
--googleDrivePath = thư mục CHA chứa tất cả session folders
```

App sẽ tạo subfolder session bên trong `--googleDrivePath`:

```
{googleDrivePath}/
  ├── 20260514_153200_abc123/    ← session 1 (app tự tạo)
  │   ├── photo_001.jpg
  │   ├── photo_002.jpg
  │   └── final_layout.jpg
  ├── 20260514_160000_def456/    ← session 2 (app tự tạo)
  │   ├── photo_001.jpg
  │   └── ...
  └── ...
```

#### Mối quan hệ: `--googleDrivePath` ↔ Google Drive ↔ Apps Script

Đây là phần **dễ nhầm nhất**. Có 3 thứ phải khớp với nhau:

```
                    MÁY TÍNH (local)                           GOOGLE DRIVE (cloud)
                    ─────────────────                          ──────────────────────
--googleDrivePath = ".../My Drive/PhotoBooth"         ←sync→  My Drive/PhotoBooth/
                         ↓                                          ↓
                    App tạo subfolder:                         Drive sync lên:
                    ".../My Drive/PhotoBooth/20260514_xxx"      PhotoBooth/20260514_xxx
                                                                    ↓
                                                               Apps Script tìm:
                                                               PhotoBooth → 20260514_xxx ✅
```

> ⚠️ **Quy tắc vàng:** Apps Script tìm folder session **bên trong folder tên `PhotoBooth`** trên Google Drive.
> Nên `--googleDrivePath` **PHẢI kết thúc** bằng đường dẫn tới folder `PhotoBooth` trên máy (nơi Google Drive Desktop đã sync).

#### Ví dụ cụ thể — 3 cách đặt path

**✅ Cách 1: Folder `PhotoBooth` ở root Google Drive (KHUYÊN DÙNG)**

```
Google Drive web: My Drive/PhotoBooth/
Local path:       .../My Drive/PhotoBooth
```

Lệnh chạy:
```bash
--googleDrivePath="/Users/huynguyen/Library/CloudStorage/GoogleDrive-dhgaming12th4@gmail.com/My Drive/PhotoBooth"
```

**✅ Cách 2: Folder `PhotoBooth` nằm trong subfolder (ví dụ `sending`)**

```
Google Drive web: My Drive/sending/PhotoBooth/
Local path:       .../My Drive/sending/PhotoBooth
```

Lệnh chạy:
```bash
--googleDrivePath="/Users/huynguyen/Library/CloudStorage/GoogleDrive-dhgaming12th4@gmail.com/My Drive/sending/PhotoBooth"
```

> ⚠️ Nếu dùng cách này, bạn cần sửa Apps Script để tìm đúng folder cha. Xem [Mục 3.5](#35-tuỳ-chỉnh-vị-trí-folder-trên-google-drive-nâng-cao).

**❌ Cách SAI: Path không chứa folder `PhotoBooth`**

```bash
# SAI — App sẽ tạo session trực tiếp trong "My Drive", không nằm trong PhotoBooth
--googleDrivePath="/Users/huynguyen/Library/CloudStorage/GoogleDrive-dhgaming12th4@gmail.com/My Drive"
```

```
Kết quả: My Drive/20260514_xxx/  ← Apps Script tìm TRONG PhotoBooth → KHÔNG THẤY → QR lỗi!
```

### 3.4. Tạo folder PhotoBooth trên Google Drive

1. Mở Google Drive web: https://drive.google.com
2. Ở root (My Drive), tạo folder tên **`PhotoBooth`**
3. ⚠️ Đảm bảo **CHỈ CÓ DUY NHẤT 1 folder** tên `PhotoBooth` trên toàn bộ Drive
4. Chờ Google Drive Desktop sync folder này về máy (thường mất 30 giây - 1 phút)

> Folder này sẽ chứa các subfolder session (mỗi lần chụp = 1 subfolder).
> App sẽ tự tạo subfolder bên trong — bạn KHÔNG cần tạo thủ công.

### 3.5. Tuỳ chỉnh vị trí folder trên Google Drive (Nâng cao)

Mặc định, Apps Script tìm folder tên `PhotoBooth` **ở root Drive (My Drive)**:

```javascript
// Trong Apps Script — dòng này quyết định folder cha
var parents = DriveApp.getFoldersByName("PhotoBooth");
```

**Nếu bạn muốn đặt folder ở vị trí khác** (ví dụ bên trong folder `sending`), cần sửa Apps Script:

```javascript
// Thay dòng:
var parents = DriveApp.getFoldersByName("PhotoBooth");

// Bằng (ví dụ folder PhotoBooth nằm trong "sending"):
var sendingFolders = DriveApp.getFoldersByName("sending");
if (!sendingFolders.hasNext()) {
  return jsonResponse({ success: false, error: "'sending' folder not found" });
}
var sendingFolder = sendingFolders.next();
var parents = sendingFolder.getFoldersByName("PhotoBooth");
```

Sau khi sửa, nhớ deploy lại: **Triển khai** → **Quản lý triển khai** → **Chỉnh sửa** → **Phiên bản mới** → **Triển khai**.

### 3.6. Kiểm tra đường dẫn đúng chưa

Mở Terminal và chạy:

```bash
# Thay bằng đường dẫn thực tế của bạn
ls "/Users/huynguyen/Library/CloudStorage/GoogleDrive-dhgaming12th4@gmail.com/My Drive/PhotoBooth"
```

Nếu thấy nội dung folder (hoặc folder trống) → **đúng rồi** ✅  
Nếu báo `No such file or directory` → kiểm tra lại:
- Đường dẫn Google Drive có đúng không? (xem [Bước 3.2](#32-tìm-đường-dẫn-google-drive-trên-máy))
- Folder `PhotoBooth` đã tạo trên Google Drive web chưa? (xem [Bước 3.4](#34-tạo-folder-photobooth-trên-google-drive))
- Google Drive Desktop đang chạy và đã sync xong chưa?

### 3.7. Bảng tóm tắt — Chọn đường dẫn đúng

| Bạn muốn | `--googleDrivePath` trỏ tới | Folder trên Drive web | Apps Script tìm |
|-----------|---------------------------|----------------------|-----------------|
| Lưu vào `My Drive/PhotoBooth/` | `.../My Drive/PhotoBooth` | Tạo `PhotoBooth` ở root | Mặc định — không sửa |
| Lưu vào `My Drive/sending/PhotoBooth/` | `.../My Drive/sending/PhotoBooth` | Tạo `sending/PhotoBooth` | Cần sửa script (xem 3.5) |
| Lưu vào ổ cứng (không Google Drive) | Không truyền | Không cần | Không cần — dùng kịch bản A |

---

## 4. Cấu hình Google Apps Script (chi tiết từng bước)

### Apps Script là gì?

Apps Script là **dịch vụ miễn phí** của Google cho phép bạn chạy code JavaScript trên cloud. Mình dùng nó như một **web API** để:
- Nhận tên folder session
- Tìm folder đó trên Google Drive
- Set quyền chia sẻ "Anyone with link can view"
- Trả về link folder

**Không cần:** Google Cloud Console, thẻ tín dụng, kiến thức lập trình.

---

### Bước 4.1: Tạo Apps Script project

1. Mở Google Drive web: https://drive.google.com
2. Bấm **+ Mới** (nút New ở góc trái trên)
3. Chọn **Thêm** → **Google Apps Script**

   > Nếu không thấy "Google Apps Script", thử:
   > - Bấm **+ Mới** → **Thêm** → **Kết nối thêm ứng dụng** → tìm "Google Apps Script" → cài đặt

4. Một tab mới mở ra với editor Apps Script
5. Đổi tên project: Click vào "Untitled project" ở góc trái trên → gõ: **`PhotoBooth-LinkService`**

---

### Bước 4.2: Bật Advanced Drive API

> ⚠️ Bước này **BẮT BUỘC** — nếu không bật, script sẽ không thể share folder.

1. Trong Apps Script editor, bấm **Dịch vụ** (biểu tượng `+` bên sidebar trái, phía dưới "Files")
2. Tìm **Drive API** → chọn
3. Giữ nguyên tên identifier là `Drive` → bấm **Add**
4. Bạn sẽ thấy "Drive" xuất hiện trong sidebar dưới mục "Services"

---

### Bước 4.3: Paste code vào file Code.gs

1. Xoá toàn bộ code mặc định trong file `Code.gs` (thường là `function myFunction() {}`)
2. Copy **TOÀN BỘ** đoạn code sau và paste vào:

```javascript
/**
 * PhotoBooth Link Service
 * Nhận tên folder session → tìm trên Drive → set public → trả link
 * 
 * Yêu cầu: Bật Advanced Drive API (sidebar Services → Drive API)
 */

function doGet(e) {
  try {
    var folderName = e.parameter.folder;
    
    if (!folderName) {
      return jsonResponse({ success: false, error: "Missing 'folder' parameter" });
    }
    
    // Tìm folder PhotoBooth (folder cha)
    var parents = DriveApp.getFoldersByName("PhotoBooth");
    if (!parents.hasNext()) {
      return jsonResponse({ success: false, error: "PhotoBooth folder not found on Drive" });
    }
    var parentFolder = parents.next();
    
    // Tìm session folder bên trong PhotoBooth
    var sessionFolders = parentFolder.getFoldersByName(folderName);
    if (!sessionFolders.hasNext()) {
      return jsonResponse({ 
        success: false, 
        error: "Session folder not found (may still be syncing)",
        folder: folderName
      });
    }
    var sessionFolder = sessionFolders.next();
    
    // Set quyền chia sẻ cho folder bằng Advanced Drive API
    Drive.Permissions.create(
      { role: "reader", type: "anyone" },
      sessionFolder.getId()
    );
    
    // Set quyền cho tất cả file bên trong
    var files = sessionFolder.getFiles();
    var fileCount = 0;
    while (files.hasNext()) {
      var file = files.next();
      Drive.Permissions.create(
        { role: "reader", type: "anyone" },
        file.getId()
      );
      fileCount++;
    }
    
    // Trả về link folder
    return jsonResponse({
      success: true,
      url: sessionFolder.getUrl(),
      folderName: folderName,
      fileCount: fileCount
    });
    
  } catch (err) {
    return jsonResponse({ success: false, error: err.toString() });
  }
}

function jsonResponse(data) {
  return ContentService
    .createTextOutput(JSON.stringify(data))
    .setMimeType(ContentService.MimeType.JSON);
}
```

3. Bấm **Ctrl+S** (hoặc Cmd+S trên Mac) để lưu

---

### Bước 4.4: Deploy (Triển khai) Web App

> Đây là bước quan trọng nhất — làm đúng để có URL ổn định.

1. Bấm nút **Triển khai** (Deploy) ở góc phải trên → **Triển khai mới** (New deployment)

2. Một popup hiện ra:

   | Mục | Giá trị |
   |-----|---------|
   | **Loại** | Bấm icon ⚙️ → chọn **Ứng dụng web** (Web app) |
   | **Mô tả** | `PhotoBooth Link Service` |
   | **Thực thi với tư cách** | **Tôi** (your email — `dhgaming12th4@gmail.com`) |
   | **Quyền truy cập** | **Bất kỳ ai** (Anyone) |

3. Bấm **Triển khai** (Deploy)

4. **Lần đầu tiên** — Google yêu cầu xác nhận quyền truy cập Drive:
   - Bấm **Xem xét quyền** (Review permissions)
   - Chọn tài khoản Google của bạn
   - ⚠️ Google sẽ cảnh báo "app chưa xác minh" — đây là **bình thường** vì app do bạn tự tạo:
     - Bấm **Nâng cao** (Advanced)
     - Bấm **Đi tới PhotoBooth-LinkService (không an toàn)** (Go to PhotoBooth-LinkService (unsafe))
     - Bấm **Cho phép** (Allow)

5. Sau khi deploy xong, bạn nhận được **URL** dạng:
   ```
   https://script.google.com/macros/s/AKfycbxNiDN4Dp.../exec
   ```

6. **⭐ COPY URL NÀY VÀ LƯU LẠI** — đây là giá trị bạn sẽ dùng cho `--appsScriptUrl`

---

### Bước 4.5: Test Apps Script (kiểm tra hoạt động)

Mở trình duyệt và test 3 trường hợp:

**Test 1 — Không có parameter (kiểm tra lỗi):**

Mở URL Apps Script (không thêm gì sau `exec`):
```
https://script.google.com/macros/s/AKfycb.../exec
```
✅ Kết quả mong đợi:
```json
{ "success": false, "error": "Missing 'folder' parameter" }
```

**Test 2 — Folder không tồn tại:**
```
https://script.google.com/macros/s/AKfycb.../exec?folder=test_khong_co
```
✅ Kết quả mong đợi:
```json
{ "success": false, "error": "Session folder not found (may still be syncing)", "folder": "test_khong_co" }
```

**Test 3 — Folder tồn tại (tạo thủ công để test):**

1. Vào Google Drive web → mở folder `PhotoBooth` → tạo subfolder tên `test_session`
2. Upload 1-2 ảnh bất kỳ vào `test_session`
3. Truy cập:
   ```
   https://script.google.com/macros/s/AKfycb.../exec?folder=test_session
   ```
4. ✅ Kết quả mong đợi:
   ```json
   { "success": true, "url": "https://drive.google.com/drive/folders/...", "folderName": "test_session", "fileCount": 2 }
   ```
5. **Mở URL trả về trong cửa sổ ẩn danh (Incognito)** → phải thấy folder + ảnh mà KHÔNG cần đăng nhập

> 🎉 Nếu cả 3 test đều pass → Apps Script hoạt động đúng!

---

### ⚠️ Cập nhật code Apps Script sau này

Nếu bạn cần sửa code script:

1. Sửa code trong editor
2. Bấm **Triển khai** → **Quản lý triển khai** (Manage deployments)
3. Bấm **icon bút chì** (Edit) ở deployment hiện tại
4. Chọn **Phiên bản mới** (New version)
5. Bấm **Triển khai** (Deploy)

> ⛔ **KHÔNG BAO GIỜ** bấm "Triển khai mới" (New deployment) — sẽ tạo ra URL MỚI và URL cũ bị phá!

---

## 5. Lệnh chạy app — Copy & Paste

### Kịch bản A: Local + Ngrok (không cần Google Drive)

```bash
dotnet run --project src/PhotoBooth.UI -- \
  --storeId=1 \
  --deviceId=device-1 \
  --apiBaseUrl="https://your-ngrok-url.ngrok-free.dev"
```

### Kịch bản B: Google Drive (không cần Ngrok)

```bash
dotnet run --project src/PhotoBooth.UI -- \
  --storeId=1 \
  --deviceId=device-1 \
  --googleDriveEnabled=true \
  --googleDrivePath="/Users/huynguyen/Library/CloudStorage/GoogleDrive-dhgaming12th4@gmail.com/My Drive" \
  --appsScriptUrl="https://script.google.com/macros/s/AKfycbxNiDN4DpVJswsasmaxUpubSSvI2ATO9PI_ZLbeRxl0bhX8VZF_g-FgR3SYj0MkyJjawQ/exec"
```

> 📝 **Thay thế:**
> - `--googleDrivePath` = đường dẫn Google Drive trên máy bạn (xem [Bước 3.2](#32-tìm-đường-dẫn-google-drive-trên-máy))
> - `--appsScriptUrl` = URL bạn copy ở [Bước 4.4](#bước-44-deploy-triển-khai-web-app)

### Bảng tham số đầy đủ

| Tham số | Bắt buộc | Mặc định | Mô tả |
|---------|----------|----------|-------|
| `--storeId=<số>` | Không | `null` | ID cửa hàng |
| `--deviceId=<text>` | Không | `device-1` | ID thiết bị |
| `--planType=<Basic\|Pro>` | Không | `Pro` | Gói dịch vụ |
| `--apiBaseUrl=<url>` | Không | URL ngrok mặc định | URL server API (ngrok) |
| `--priceLayout6=<số>` | Không | `70000` | Giá layout 6 ảnh (VNĐ) |
| `--priceLayout2=<số>` | Không | `50000` | Giá layout 2 ảnh (VNĐ) |
| `--googleDriveEnabled=<true\|false>` | Không | `false` | Bật/tắt lưu ảnh vào Google Drive |
| `--googleDrivePath=<path>` | Khi GDrive bật | `""` | Đường dẫn thư mục Google Drive trên máy |
| `--appsScriptUrl=<url>` | Khi GDrive bật | `""` | URL Google Apps Script web app |

---

## 6. Bản đồ file code — Vị trí lưu ảnh được điều khiển ở đâu?

Nếu bạn muốn **tự tay thay đổi nơi lưu ảnh** hoặc hiểu hệ thống hoạt động ra sao, đây là bản đồ:

### 📋 Tổng quan nhanh

```
Ảnh được lưu ở đâu?
  ↓
SessionService.cs quyết định BASE PATH (folder cha)
  ↓
SessionService.cs tạo subfolder session bên trong base path
  ↓
CaptureViewModel.cs lưu ảnh vào subfolder đó
```

### 📁 File 1: `DeviceConfig.cs` — Nơi đọc tham số từ dòng lệnh

**Đường dẫn:** [src/PhotoBooth.UI/DeviceConfig.cs](file:///Users/huynguyen/work/projects/ptb/src/PhotoBooth.UI/DeviceConfig.cs)

**Vai trò:** Đọc và lưu các giá trị `--googleDriveEnabled`, `--googleDrivePath`, `--appsScriptUrl` khi bạn truyền từ command line.

**Các dòng quan trọng:**

```csharp
// Dòng 21-23: Khai báo giá trị mặc định
public static bool GoogleDriveEnabled { get; set; } = false;    // Mặc định: TẮT
public static string GoogleDrivePath { get; set; } = "";         // Mặc định: rỗng
public static string AppsScriptUrl { get; set; } = "";           // Mặc định: rỗng
```

```csharp
// Dòng 93-108: Đọc giá trị từ CLI arguments
else if (arg.StartsWith("--googleDriveEnabled="))    // ← đọc true/false
else if (arg.StartsWith("--googleDrivePath="))       // ← đọc đường dẫn folder
else if (arg.StartsWith("--appsScriptUrl="))         // ← đọc URL Apps Script
```

> 💡 **Muốn đổi giá trị mặc định?** Sửa dòng 21-23. Ví dụ muốn mặc định luôn bật Google Drive:
> ```csharp
> public static bool GoogleDriveEnabled { get; set; } = true;  // Đổi false → true
> public static string GoogleDrivePath { get; set; } = "/Users/huynguyen/Library/CloudStorage/GoogleDrive-dhgaming12th4@gmail.com/My Drive/PhotoBooth";
> ```
> Sau khi sửa, **không cần truyền `--googleDriveEnabled` và `--googleDrivePath` nữa** vì đã có default.

---

### 📁 File 2: `SessionService.cs` — Nơi quyết định BASE PATH (thư mục gốc lưu ảnh)

**Đường dẫn:** [src/PhotoBooth.UI/Services/SessionService.cs](file:///Users/huynguyen/work/projects/ptb/src/PhotoBooth.UI/Services/SessionService.cs)

**Vai trò:** Chọn thư mục gốc (`base path`) dựa trên config. Tất cả session folders sẽ được tạo bên trong thư mục này.

**Logic quyết định (dòng 21-35):**

```csharp
private static string SessionsBaseDirectory
{
    get
    {
        // Nếu Google Drive BẬT + path có giá trị + folder tồn tại trên máy
        if (GoogleDriveEnabled && GoogleDrivePath không rỗng)
        {
            if (folder tồn tại trên máy)
                return GoogleDrivePath;           // ← LƯU VÀO GOOGLE DRIVE
            else
                log warning "path not found"      // ← folder không tồn tại
        }
        return ~/Pictures/PhotoBooth;             // ← FALLBACK: LƯU VÀO Ổ CỨNG
    }
}
```

**Tóm lại:**

| Điều kiện | Ảnh lưu vào |
|-----------|-------------|
| `GoogleDriveEnabled = true` + `GoogleDrivePath` hợp lệ + folder tồn tại | `GoogleDrivePath` |
| `GoogleDriveEnabled = true` + folder KHÔNG tồn tại | `~/Pictures/PhotoBooth/` (fallback) |
| `GoogleDriveEnabled = false` (mặc định) | `~/Pictures/PhotoBooth/` |

> 💡 **Muốn đổi folder mặc định khi không dùng Google Drive?** Sửa dòng 33:
> ```csharp
> // Thay dòng 33:
> return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "PhotoBooth");
> 
> // Bằng (ví dụ lưu vào Desktop):
> return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "PhotoBooth");
> 
> // Hoặc hardcode path tuỳ ý:
> return "/Users/huynguyen/Documents/MyPhotoBooth";
> ```

---

### 📁 File 3: `SessionService.cs` — Nơi tạo subfolder session

**Cùng file:** [SessionService.cs dòng 57-73](file:///Users/huynguyen/work/projects/ptb/src/PhotoBooth.UI/Services/SessionService.cs#L57-L73)

**Vai trò:** Tạo subfolder session (ví dụ `20260514_153200_abc123`) bên trong base path.

```csharp
// Dòng 57-66: Tạo session folder
public void PrepareSessionDirectory()
{
    // Tên folder = timestamp + random ID
    var sessionFolder = $"{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid()...}";
    
    var basePath = GetSessionsBaseDirectory();     // ← lấy base path (Google Drive hoặc local)
    var fullPath = Path.Combine(basePath, sessionFolder);  // ← ghép thành path đầy đủ
    Directory.CreateDirectory(fullPath);            // ← tạo folder trên máy
    
    CurrentSession.SessionDirectory = fullPath;    // ← lưu path vào session
    CurrentSession.SessionFolderName = sessionFolder;  // ← lưu tên folder (cho Apps Script)
}
```

**Kết quả:** Folder tạo ra có dạng:
```
{base_path}/20260514_153200_abc123/
```

---

### 📁 File 4: `CaptureViewModel.cs` — Nơi thực sự lưu ảnh

**Đường dẫn:** [src/PhotoBooth.UI/ViewModels/CaptureViewModel.cs](file:///Users/huynguyen/work/projects/ptb/src/PhotoBooth.UI/ViewModels/CaptureViewModel.cs)

**Vai trò:** Chụp ảnh + lưu file vào session folder.

```csharp
// Dòng 88-96: Lấy session directory (đã được PrepareSessionDirectory tạo sẵn)
if (string.IsNullOrEmpty(SessionService.CurrentSession.SessionDirectory))
{
    SessionService.PrepareSessionDirectory();  // ← tạo folder nếu chưa có
}
_photosDirectory = SessionService.CurrentSession.SessionDirectory!;  // ← lấy path
Directory.CreateDirectory(_photosDirectory);   // ← đảm bảo folder tồn tại

// Dòng 413: Lưu ảnh vào folder
var photoPath = _cameraService.CapturePhoto(_photosDirectory);  // ← LƯU ẢNH TẠI ĐÂY
```

---

### 📁 File 5: `GoogleDriveQRService.cs` — Nơi tạo QR code từ Google Drive link

**Đường dẫn:** [src/PhotoBooth.UI/Services/GoogleDriveQRService.cs](file:///Users/huynguyen/work/projects/ptb/src/PhotoBooth.UI/Services/GoogleDriveQRService.cs)

**Vai trò:** Gọi Apps Script API → lấy link share → tạo QR code.

```csharp
// Dòng 64-65: Gọi Apps Script với tên session folder
var url = $"{DeviceConfig.AppsScriptUrl}?folder={sessionFolderName}";
// Apps Script sẽ tìm folder này trong "PhotoBooth" trên Google Drive
```

> 💡 **Nếu folder trên Drive có tên khác "PhotoBooth"**, bạn cần sửa cả **Apps Script** (file Code.gs trên Google Drive) — xem [Phần 4](#4-cấu-hình-google-apps-script-chi-tiết-từng-bước).

---

### 🗺️ Sơ đồ tổng hợp: File nào → Làm gì

```
Lệnh chạy app:
  --googleDriveEnabled=true
  --googleDrivePath="/path/to/drive/PhotoBooth"
  --appsScriptUrl="https://script.google.com/..."
        │
        ▼
  DeviceConfig.cs          ← Đọc + lưu tham số
        │
        ▼
  SessionService.cs        ← Chọn base path (Drive hay local?)
        │                  ← Tạo subfolder session
        ▼
  CaptureViewModel.cs      ← Lưu ảnh vào session folder
        │
        ▼
  GoogleDriveQRService.cs   ← Gọi Apps Script, tạo QR
        │
        ▼
  Apps Script (Code.gs)     ← Tìm folder trên Drive, share, trả link
    trên Google Drive web
```

---

## 7. Xử lý sự cố (Troubleshooting)

### ❓ QR hiện "Không thể đồng bộ ảnh lên Google Drive"

**Nguyên nhân:** Google Drive Desktop chưa sync file lên cloud kịp thời (app chờ tối đa ~30 giây).

**Cách sửa:**
1. Kiểm tra Google Drive Desktop đang chạy (biểu tượng trên taskbar/menu bar)
2. Kiểm tra internet
3. Thử chụp lại — lần sau file sẽ sync nhanh hơn

---

### ❓ QR hiện "Chưa cấu hình Apps Script URL"

**Nguyên nhân:** Bạn bật `--googleDriveEnabled=true` nhưng quên truyền `--appsScriptUrl`.

**Cách sửa:** Thêm `--appsScriptUrl="https://script.google.com/macros/s/..."` vào lệnh chạy.

---

### ❓ Apps Script trả về "PhotoBooth folder not found on Drive"

**Nguyên nhân:** Không có folder tên `PhotoBooth` ở root Google Drive.

**Cách sửa:**
1. Mở https://drive.google.com
2. Tạo folder tên `PhotoBooth` ở root (My Drive)
3. Đảm bảo chỉ có **1 folder duy nhất** tên `PhotoBooth`

---

### ❓ Ảnh lưu vào ~/Pictures/PhotoBooth/ thay vì Google Drive

**Nguyên nhân:** Fallback — xảy ra khi:
- `--googleDriveEnabled=false` (hoặc không truyền)
- `--googleDrivePath` trỏ đến thư mục không tồn tại
- Google Drive Desktop chưa chạy

**Kiểm tra:**
```bash
# Kiểm tra đường dẫn có đúng không
ls "/Users/huynguyen/Library/CloudStorage/GoogleDrive-dhgaming12th4@gmail.com/My Drive"
```

---

### ❓ Google yêu cầu authorize lại khi deploy Apps Script

Đây là **bình thường** khi bạn sửa code và deploy version mới. Google muốn đảm bảo bạn đồng ý với quyền mới. Chỉ cần bấm Allow lại.

---

### ❓ Scan QR nhưng Google Drive báo "Yêu cầu quyền truy cập"

**Nguyên nhân:** Apps Script chưa set quyền share thành công.

**Cách kiểm tra:**
1. Mở link folder trong cửa sổ **Incognito** (ẩn danh)
2. Nếu không mở được → script chưa chạy đúng
3. Test lại Apps Script URL trong trình duyệt (xem [Bước 4.5](#bước-45-test-apps-script-kiểm-tra-hoạt-động))

---

### ❓ Muốn chuyển từ Google Drive về Local (hoặc ngược lại)

Chỉ cần đổi tham số khi chạy app — **không cần sửa code:**

```bash
# Chuyển sang Local: bỏ 3 tham số Google Drive
dotnet run --project src/PhotoBooth.UI -- --storeId=1 --deviceId=device-1

# Chuyển sang Google Drive: thêm 3 tham số Google Drive
dotnet run --project src/PhotoBooth.UI -- --storeId=1 --deviceId=device-1 \
  --googleDriveEnabled=true \
  --googleDrivePath="..." \
  --appsScriptUrl="..."
```

---

## Sơ đồ tóm tắt toàn bộ

```
┌──────────────────────────────────────────────────────────────────────┐
│                     BẠN CẦN LÀM GÌ?                                │
│                                                                      │
│  Bước 1: Chọn kịch bản (A: Local hay B: Google Drive)               │
│                                                                      │
│  Nếu kịch bản A (Local):                                            │
│    → Không cần làm gì thêm. Chạy app với --apiBaseUrl               │
│                                                                      │
│  Nếu kịch bản B (Google Drive):                                     │
│    → Bước 2: Cài Google Drive Desktop + đăng nhập                    │
│    → Bước 3: Tìm đường dẫn Google Drive trên máy                    │
│    → Bước 4: Tạo folder "PhotoBooth" trên Drive web                  │
│    → Bước 5: Tạo Apps Script (paste code + deploy + copy URL)        │
│    → Bước 6: Chạy app với 3 tham số Google Drive                    │
│                                                                      │
│  XONG! 🎉                                                           │
└──────────────────────────────────────────────────────────────────────┘
```
