# 📸 PhotoBooth

Hệ thống Photo Booth tự phục vụ — chụp ảnh, chọn khung, in ảnh và chia sẻ qua QR code. Được xây dựng bằng **.NET 10** với **Avalonia UI** cho giao diện desktop cross-platform và **ASP.NET Core** cho backend API.

---

## 🏗️ Kiến trúc hệ thống

```
┌─────────────────────────────────────────────────────────┐
│                    PhotoBooth Solution                   │
├─────────────────┬───────────────────┬───────────────────┤
│  PhotoBooth.UI  │ PhotoBooth.Admin  │  PhotoBooth.API   │
│  (Kiosk App)    │ (Quản trị App)    │  (REST API)       │
├─────────────────┴───────────────────┼───────────────────┤
│       PhotoBooth.Infrastructure     │                   │
│       (Camera, In ấn, Xử lý ảnh)   │                   │
├─────────────────────────────────────┤                   │
│           PhotoBooth.Core           │                   │
│     (Models, Interfaces chung)      │                   │
└─────────────────────────────────────┴───────────────────┘
```

### Các project

| Project | Mô tả | Công nghệ |
|---------|--------|------------|
| **PhotoBooth.Core** | Domain models & interfaces dùng chung | .NET 10 |
| **PhotoBooth.Infrastructure** | Services xử lý camera, ảnh, in ấn | .NET 10, OpenCvSharp4 |
| **PhotoBooth.UI** | Ứng dụng kiosk cho khách hàng sử dụng | Avalonia UI 11.3, CommunityToolkit.Mvvm |
| **PhotoBooth.Admin** | Ứng dụng quản trị cho chủ cửa hàng | Avalonia UI 11.3, DataGrid |
| **PhotoBooth.API** | REST API backend, quản lý dữ liệu | ASP.NET Core, EF Core, SQLite |
| **PhotoBooth.Tests** | Unit tests | xUnit |

---

## ✨ Tính năng chính

### 🎬 Ứng dụng Kiosk (PhotoBooth.UI)
- **Chọn layout** — Hỗ trợ layout 2 ảnh và 6 ảnh
- **Chọn khung** — Nhiều mẫu khung trang trí
- **Chọn background** — Hình nền tùy chỉnh
- **Chụp ảnh** — Kết nối camera qua OpenCV
- **Chọn & sắp xếp ảnh** — Chọn ảnh đẹp nhất từ các lần chụp
- **Thêm sticker** — Trang trí ảnh với sticker kéo thả
- **Thanh toán** — Tích hợp quy trình thanh toán
- **In ảnh** — In trực tiếp qua máy in kết nối
- **Chia sẻ QR** — Tạo QR code để tải ảnh về điện thoại
- **Google Drive sync** — Tự động backup ảnh lên Google Drive

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

> Mật khẩu mặc định cho tất cả tài khoản: xem trong source code (`Program.cs`).

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
| `--printerName` | Tên máy in | `""` |

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
