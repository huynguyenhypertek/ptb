# 📸 PhotoBooth

Hệ thống Photo Booth tự phục vụ — chụp ảnh, chọn khung, in ảnh và chia sẻ qua QR code. Được xây dựng bằng **.NET 10** với **Avalonia UI** cho giao diện desktop cross-platform và **ASP.NET Core** cho backend API.

---

## 🏗️ Kiến trúc hệ thống

```
┌─────────────────────────────────────────────────────────┐
│                    PhotoBooth Solution                   │
├───────────────┬───────────────────┬───────────────┬──────┤
│ PhotoBooth.UI │ PhotoBooth.Event  │PhotoBooth.Admin│ API  │
│ (Kiosk App)   │ (Event Fast Kiosk)│(Quản trị App)  │(REST)│
├───────────────┴───────────────────┴───────────────┼──────┤
│       PhotoBooth.Infrastructure     │                   │
│       (Camera, In ấn, Xử lý ảnh)   │                   │
├─────────────────────────────────────┤                   │
│           PhotoBooth.Core           │                   │
│     (Models, Interfaces chung)      │

                   │
└─────────────────────────────────────┴───────────────────┘
```

### Các project

| Project | Mô tả | Công nghệ |
|---------|--------|------------|
| **PhotoBooth.Core** | Domain models & interfaces dùng chung | .NET 10 |
| **PhotoBooth.Infrastructure** | Services xử lý camera, ảnh, in ấn | .NET 10, OpenCvSharp4 |
| **PhotoBooth.UI** | Ứng dụng kiosk đầy đủ (Thanh toán, Sticker, v.v) | Avalonia UI 11.3 |
| **PhotoBooth.Event** | Ứng dụng kiosk bản thu gọn cho sự kiện (Chụp nhanh, In ngay) | Avalonia UI 11.3 |
| **PhotoBooth.Admin** | Ứng dụng quản trị (Có nút khởi động UI hoặc Event) | Avalonia UI 11.3 |
| **PhotoBooth.API** | REST API backend, quản lý dữ liệu | ASP.NET Core, EF Core, SQLite |
| **PhotoBooth.Tests** | Unit tests | xUnit |

---

## ✨ Tính năng chính

### 🎬 Ứng dụng Kiosk (PhotoBooth.UI)
- **Kiosk Tiêu Chuẩn**: Đầy đủ tính năng chọn khung, sticker, quét mã thanh toán, chia sẻ QR.
- **Kiosk Sự Kiện (PhotoBooth.Event)**: Quy trình rút gọn tối đa (Chỉ còn 4 bước: Start -> Capture 8 ảnh -> Select 4 ảnh -> In & Chia sẻ QR). Phù hợp cho event đông người, cần chụp nhanh.
- **Kết nối Camera** — Hỗ trợ nhiều dòng camera qua OpenCV.
- **In ảnh mượt mà** — Hỗ trợ in qua DNP DS-RX1HS (với tính năng fit-to-page tự động xoay ngang dọc), HP, và các dòng máy in hệ thống.
- **Google Drive Sync** — Upload ảnh ngay lập tức và tạo mã QR tải ảnh siêu tốc.

### 🖥️ Ứng dụng Quản trị (PhotoBooth.Admin)
- **Dashboard** — Tổng quan hoạt động
- **Quản lý cửa hàng** — CRUD stores
- **Quản lý người dùng** — Phân quyền SystemAdmin / StoreAdmin / Device
- **Quản lý khung ảnh** — Upload & quản lý frame templates
- **Quản lý gói dịch vụ** — Subscription plans (Basic, Pro, Premium)
- **Khởi động thiết bị** — Launch/Stop PhotoBooth UI instances từ xa
- **Cài đặt** — Cấu hình Google Drive, máy in, API URL

### 🌐 REST API (PhotoBooth.API)
- **Auth** — JWT authentication
- **Users** — Quản lý tài khoản & phân quyền (SystemAdmin, StoreAdmin, Device)
- **Stores** — Quản lý chuỗi cửa hàng
- **Frames** — Upload & quản lý khung ảnh
- **Sessions** — Theo dõi phiên chụp ảnh
- **Photos** — Lưu trữ & chia sẻ ảnh qua mã code
- **Subscription Plans** — Quản lý gói dịch vụ

---

## 📋 Yêu cầu hệ thống

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Camera USB (cho PhotoBooth.UI)
- Máy in (tùy chọn, cho tính năng in ảnh)
- Google Drive Desktop (tùy chọn, cho tính năng sync ảnh)

---

## 🚀 Cài đặt & Chạy

### 1. Clone repository

```bash
git clone <repository-url>
cd ptb
```

### 2. Cấu hình API

#### Tạo JWT Secret Key (bắt buộc)

```bash
cd src/PhotoBooth.API
dotnet user-secrets set "Jwt:Key" "your-secret-key-must-be-at-least-32-characters-long"
```

> ⚠️ **Lưu ý:** Key phải dài ít nhất 32 ký tự và không chứa giá trị placeholder như `CHANGE_ME` hay `SuperSecret`.

#### Cấu hình CORS (tùy chỉnh)

Chỉnh sửa `src/PhotoBooth.API/appsettings.json`:

```json
{
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:3000",
      "http://localhost:5148"
    ]
  }
}
```

### 3. Chạy API Server

```bash
dotnet run --project src/PhotoBooth.API
```

API sẽ chạy tại `http://localhost:5148`. Lần đầu chạy, hệ thống tự động:
- Tạo database SQLite (`photobooth.db`)
- Seed dữ liệu mẫu:

| Tài khoản | Vai trò | Mô tả |
|-----------|---------|--------|
| `admin` | SystemAdmin | Quản lý toàn hệ thống |
| `adminCH1` | StoreAdmin | Quản lý Cửa hàng 1 |
| `Pb1_Ch1` | Device | Máy chụp 1, Cửa hàng 1 |

> 🔑 **Mật khẩu mặc định cho tất cả tài khoản:** `admin123`

### 4. Chạy Admin App

```bash
dotnet run --project src/PhotoBooth.Admin
```

Đăng nhập bằng tài khoản `admin` hoặc `adminCH1`, sau đó có thể:
- Quản lý cửa hàng, người dùng, khung ảnh
- Khởi động PhotoBooth UI cho từng thiết bị

### 5. Chạy PhotoBooth UI (trực tiếp)

```bash
dotnet run --project src/PhotoBooth.UI -- \
  --deviceId=Pb1_Ch1 \
  --storeId=1 \
  --apiBaseUrl=http://localhost:5148
```

#### Tham số dòng lệnh

| Tham số | Mô tả | Mặc định |
|---------|--------|----------|
| `--deviceId` | ID thiết bị (username) | `device-1` |
| `--storeId` | ID cửa hàng | `null` |
| `--apiBaseUrl` | URL API server | `http://localhost:5148` |
| `--planType` | Gói dịch vụ (`Basic` / `Pro`) | `Pro` |
| `--priceLayout6` | Giá layout 6 ảnh (VNĐ) | `70000` |
| `--priceLayout2` | Giá layout 2 ảnh (VNĐ) | `50000` |
| `--googleDriveEnabled` | Bật Google Drive sync | `true` |
| `--googleDrivePath` | Đường dẫn folder Google Drive | `""` |
| `--appsScriptUrl` | URL Google Apps Script | `""` |
| `--enablePrinting` | Bật tính năng in ảnh | `false` |
| `--printerName` | Tên máy in (Bỏ trống để in bằng máy mặc định) | `""` |
| `--countdownSeconds` | Thời gian đếm ngược trước mỗi lần nháy máy (giây) | `3` |
| `--eventName` | Tên sự kiện (Dùng làm tiền tố cho tên file ảnh) | `DONGFEST` |

### 6. Chạy PhotoBooth Event (trực tiếp)

```bash
dotnet run --project src/PhotoBooth.Event -- \
  --deviceId=Pb1_Ch1 \
  --storeId=1 \
  --apiBaseUrl=http://localhost:5148 \
  --enablePrinting=true \
  --countdownSeconds=3 \
  --eventName=DONGFEST
```

#### Tham số Event

| Tham số | Mô tả | Mặc định |
|---------|--------|----------|
| `--deviceId` | ID thiết bị (username) | `device-1` |
| `--storeId` | ID cửa hàng | `null` |
| `--apiBaseUrl` | URL API server | `http://localhost:5148` |
| `--enablePrinting` | Bật tính năng in ảnh | `false` |
| `--printerName` | Tên máy in (Bỏ trống = máy in mặc định) | `""` |
| `--countdownSeconds` | Đếm ngược trước mỗi ảnh (giây) | `3` |
| `--eventName` | Tên sự kiện (tiền tố file ảnh) | `DONGFEST` |
| `--qrCodeSizePercent` | Kích thước QR code (% chiều cao ảnh, 1-30) | `10` |
| `--googleDriveEnabled` | Bật Google Drive sync | `true` |
| `--googleDrivePath` | Đường dẫn folder Google Drive | `""` |
| `--appsScriptUrl` | URL Google Apps Script | `""` |

---

## 🖨️ Kiểm tra chức năng In ảnh

macOS sử dụng hệ thống **CUPS** để quản lý in. Các lệnh hữu ích:

```bash
# Xem danh sách máy in đang kết nối
lpstat -p

# Xem hàng đợi in (print queue)
lpstat -o

# Test in thử 1 file ảnh
lp -d <TenMayIn> /path/to/image.jpg

# Xem lịch sử jobs đã hoàn thành
lpstat -W completed | tail -10
```

**Giao diện web CUPS:** Mở trình duyệt tại `http://localhost:631`
- Xem trạng thái máy in
- Xem/hủy print jobs
- Kiểm tra lỗi in

> 💡 **Tip:** Nếu `lpstat -o` có jobs ở trạng thái "pending" → máy in chưa kết nối. Khi kết nối sẽ tự động in.

---

## 📦 Triển khai sang máy Mac mới

### Yêu cầu phần cứng

| Thiết bị | Bắt buộc? | Ghi chú |
|----------|-----------|---------|
| Mac (Intel x64 hoặc Apple Silicon) | ✅ | macOS 13+ khuyến nghị |
| Camera USB hoặc HDMI Capture Card | ✅ | Capture card HDMI→USB được macOS nhận tự động (UVC) |
| Máy in ảnh (DNP DS-RX1HS, v.v.) | Tùy chọn | Kết nối USB, cần cài driver riêng |
| Màn hình cảm ứng hoặc iPad | Tùy chọn | Dùng làm màn hình khách tương tác |

### Bước 1: Cài đặt phần mềm cần thiết

#### .NET 10 SDK (bắt buộc)

```bash
# Cách 1: Tải installer từ Microsoft (khuyến nghị)
# Vào: https://dotnet.microsoft.com/download/dotnet/10.0
# Chọn đúng kiến trúc:
#   - Mac Intel     → macOS x64
#   - Mac M1/M2/M3  → macOS Arm64

# Cách 2: Dùng script có sẵn trong repo
./dotnet-install.sh --channel 10.0

# Cách 3: Homebrew
brew install dotnet

# Kiểm tra sau khi cài:
dotnet --version   # Phải hiện 10.x.x
```

#### Git (nếu chưa có)

```bash
# macOS thường có sẵn, kiểm tra:
git --version

# Nếu chưa có, cài qua Xcode Command Line Tools:
xcode-select --install
```

#### Google Drive Desktop (tùy chọn — chỉ khi dùng tính năng sync ảnh)

1. Tải từ https://www.google.com/drive/download/
2. Cài đặt và đăng nhập bằng **cùng tài khoản Gmail** sở hữu Apps Script
3. Tạo folder `PhotoBooth` trong Google Drive
4. Ghi nhớ đường dẫn local, ví dụ: `/Users/<tên>/Library/CloudStorage/GoogleDrive-<email>/Drive của tôi/PhotoBooth/`

> ⚠️ **Quan trọng:** Đường dẫn Google Drive trên máy và Apps Script URL **phải cùng 1 tài khoản Gmail**. Nếu khác account, app sẽ không tìm thấy folder đã sync.

#### Driver máy in (tùy chọn — chỉ khi in ảnh)

- **DNP DS-RX1HS**: Tải driver từ [dnpphoto.com/support/downloads](https://www.dnpphoto.com/en-us/support/downloads/)
- **Máy in khác**: Thêm trong **System Settings → Printers & Scanners**
- Kiểm tra máy in đã nhận:
  ```bash
  lpstat -p          # Xem danh sách máy in
  lpstat -p -d       # Xem máy in mặc định
  ```

### Bước 2: Clone và cấu hình project

```bash
git clone <repository-url>
cd ptb
```

#### Cấu hình JWT Secret (bắt buộc cho API)

```bash
cd src/PhotoBooth.API
dotnet user-secrets set "Jwt:Key" "your-secret-key-must-be-at-least-32-characters-long"
cd ../..
```

> ⚠️ Key phải dài ít nhất 32 ký tự.

### Bước 3: Chạy hệ thống

#### Cách A: Chạy từ source (dev mode)

source ~/.bash_profile

```bash
# Terminal 1: Chạy API (tự tạo DB + tài khoản mặc định)
dotnet run --project src/PhotoBooth.API

# Terminal 2: Chạy Admin (đăng nhập rồi khởi động Event/UI app từ giao diện)
    dotnet run --project src/PhotoBooth.Admin
```

#### Cách B: Build app bundle (production)

```bash
chmod +x build-mac-app.sh
./build-mac-app.sh
# → Tạo PhotoBoothAdmin.app (tự nhận diện Intel x64 / Apple Silicon arm64)

# Chạy:
open PhotoBoothAdmin.app
```

> 💡 Lần đầu mở app trên macOS có thể bị chặn bởi Gatekeeper. Vào **System Settings → Privacy & Security** → nhấn **Open Anyway**.

### Bước 4: Đăng nhập Admin và cấu hình

| Tài khoản | Mật khẩu | Vai trò |
|-----------|----------|--------|
| `admin` | `admin123` | SystemAdmin |
| `adminCH1` | `admin123` | StoreAdmin |
| `Pb1_Ch1` | `admin123` | Device |

> Database SQLite tự động tạo khi chạy API lần đầu, **không cần cài database riêng**.

Sau khi đăng nhập Admin, vào tab **Cài đặt** để cấu hình:
- **Google Drive**: Bật/tắt, đường dẫn folder, Apps Script URL
- **Máy in**: Bật/tắt, tên máy in (lấy từ `lpstat -p`), khổ giấy
- **Sự kiện**: Tên sự kiện, thời gian đếm ngược

### Bước 5: Cấp quyền Camera (macOS)

Lần đầu khởi động app Event/UI, macOS sẽ hỏi quyền truy cập camera → nhấn **Allow**. Nếu lỡ từ chối:

```
System Settings → Privacy & Security → Camera → bật cho PhotoBooth
```

### Xử lý sự cố thường gặp

| Vấn đề | Nguyên nhân | Cách sửa |
|--------|------------|----------|
| `dotnet: command not found` | Chưa cài .NET SDK hoặc chưa thêm vào PATH | Cài lại .NET SDK, hoặc thêm `export PATH="$HOME/.dotnet:$PATH"` vào `~/.zshrc` |
| Camera không hiện hình | Chưa cấp quyền camera | System Settings → Privacy & Security → Camera |
| Google Drive "folder not found" | Apps Script và Drive path khác account Gmail | Dùng chung 1 account cho cả hai |
| Máy in "offline" | Chưa cài driver hoặc chưa bật máy in | Cài driver, kiểm tra `lpstat -p`, mở CUPS web UI tại `http://localhost:631` |
| App bị Gatekeeper chặn | macOS chặn app chưa ký | System Settings → Privacy & Security → Open Anyway |
| Port 5148 đã bị chiếm | API instance cũ chưa tắt | `lsof -i :5148` rồi `kill <PID>` |

---

## 🧪 Chạy Tests

```bash
dotnet test
```

---

## 📁 Cấu trúc thư mục

```
ptb/
├── src/
│   ├── PhotoBooth.Core/          # Domain models & interfaces
│   │   ├── Models/               # Session, Layout, Frame, Background, Sticker
│   │   └── Interfaces/           # ICameraService, IPrintService
│   │
│   ├── PhotoBooth.Infrastructure/ # Hardware & image services
│   │   └── Services/             # CameraService, ImageCompositeService,
│   │                             # ImageCropService, PrintService
│   │
│   ├── PhotoBooth.UI/            # Kiosk app (Avalonia)
│   │   ├── Views/                # AXAML views (Start, Capture, PhotoSelection,
│   │   │                         #   FrameSelection, BackgroundSelection, Sticker,
│   │   │                         #   Payment*, ConfirmPrint, Printing, QRCode, ThankYou)
│   │   ├── ViewModels/           # MVVM ViewModels
│   │   ├── Services/             # Navigation, Session, HTTP, QR, Google Drive
│   │   ├── Converters/           # Value converters
│   │   └── Assets/               # UI assets (images, fonts)
│   │
│   ├── PhotoBooth.Admin/         # Admin app (Avalonia)
│   │   ├── Views/                # Dashboard, DeviceLauncher, FrameList,
│   │   │                         #   Login, Settings, StoreList, UserList, PlanList
│   │   ├── ViewModels/           # MVVM ViewModels
│   │   ├── Services/             # API service, Settings service
│   │   └── Converters/           # Value converters
│   │
│   └── PhotoBooth.API/           # REST API (ASP.NET Core)
│       ├── Controllers/          # Auth, Users, Stores, Frames, Sessions,
│       │                         #   Photos, SubscriptionPlans
│       ├── Models/               # EF Core entities (User, Store, Frame, Session,
│       │                         #   StoreFrame, SubscriptionPlan)
│       ├── Data/                 # AppDbContext
│       ├── Helpers/              # Utility helpers
│       └── wwwroot/              # Static files (photo download page)
│
├── tests/
│   └── PhotoBooth.Tests/         # Unit tests
│
├── assets/                       # Shared assets
│   ├── backgrounds/              # Background images
│   ├── buttons/                  # Button assets
│   └── frames/                   # Frame templates
│
├── docs/                         # Documentation
│   └── SETUP-GUIDE.md            # Hướng dẫn cài đặt chi tiết
│
└── PhotoBooth.slnx               # Solution file
```

---

## 🔐 Phân quyền

Hệ thống sử dụng 3 vai trò (roles):

| Vai trò | Quyền hạn |
|---------|-----------|
| **SystemAdmin** | Quản lý toàn bộ hệ thống, tất cả cửa hàng, tất cả người dùng |
| **StoreAdmin** | Quản lý cửa hàng được phân công, người dùng thuộc cửa hàng |
| **Device** | Tài khoản dành cho thiết bị kiosk, chỉ gửi dữ liệu session/photo |

---

## 🔄 Luồng sử dụng (User Flow)

```
Màn hình chờ → Chọn Layout → Chọn Khung → Chọn Background
     ↓
Chụp ảnh → Chọn ảnh → Thêm Sticker → Thanh toán
     ↓
Xác nhận in → In ảnh → QR Code → Cảm ơn
```

---

## 🛠️ Công nghệ sử dụng

| Công nghệ | Mục đích |
|-----------|----------|
| .NET 10 | Runtime & SDK |
| Avalonia UI 11.3 | Desktop UI framework (cross-platform) |
| ASP.NET Core 10 | REST API backend |
| Entity Framework Core 10 | ORM & database |
| SQLite | Database engine |
| OpenCvSharp4 | Camera capture & image processing |
| CommunityToolkit.Mvvm | MVVM pattern support |
| QRCoder | Tạo QR code |
| BCrypt.Net | Hash mật khẩu |
| JWT Bearer | Authentication |

---

## 📝 Ghi chú phát triển

- **Pattern**: MVVM (Model-View-ViewModel) cho cả UI và Admin app
- **Database**: SQLite (auto-created, auto-seeded khi lần đầu chạy)
- **Authentication**: JWT tokens, secret key lưu trong `dotnet user-secrets`
- **Image Processing**: OpenCvSharp4 để capture camera và xử lý ảnh
- **Printing**: Hỗ trợ in ảnh qua system printer (cấu hình qua tham số)
- **Cloud Storage**: Tùy chọn sync ảnh lên Google Drive qua Apps Script

---

## 📄 License

Private — All rights reserved.
