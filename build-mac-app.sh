#!/usr/bin/env bash
# =============================================================================
# build-mac-app.sh — Đóng gói PhotoBooth thành PhotoBoothAdmin.app cho macOS
# Yêu cầu: .NET 10 SDK, macOS (Apple Silicon M1/M2/M3)
# Sử dụng: chmod +x build-mac-app.sh && ./build-mac-app.sh
# =============================================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# ── Cấu hình ─────────────────────────────────────────────────────────────────
APP_NAME="PhotoBoothAdmin"
APP_BUNDLE="$SCRIPT_DIR/$APP_NAME.app"
RUNTIME="osx-arm64"          # M1/M2/M3 Pro — đổi thành osx-x64 nếu dùng Intel
CONFIG="Release"

SRC="$SCRIPT_DIR/src"
API_SRC="$SRC/PhotoBooth.API"
ADMIN_SRC="$SRC/PhotoBooth.Admin"
UI_SRC="$SRC/PhotoBooth.UI"
EVENT_SRC="$SRC/PhotoBooth.Event"

PUBLISH_OUT="$SCRIPT_DIR/_publish"

# ── Màu sắc terminal ──────────────────────────────────────────────────────────
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

step() { echo -e "\n${BLUE}▶ $1${NC}"; }
ok()   { echo -e "${GREEN}✓ $1${NC}"; }
warn() { echo -e "${YELLOW}⚠ $1${NC}"; }

# =============================================================================
echo ""
echo "╔══════════════════════════════════════════════════════╗"
echo "║        PhotoBooth — Mac App Builder (osx-arm64)      ║"
echo "╚══════════════════════════════════════════════════════╝"
echo ""

# ── Bước 1: Dọn dẹp output cũ ────────────────────────────────────────────────
step "Dọn dẹp output cũ..."
rm -rf "$APP_BUNDLE" "$PUBLISH_OUT"
mkdir -p "$PUBLISH_OUT"
ok "Sạch sẽ"

# ── Bước 2: Publish PhotoBooth.API ───────────────────────────────────────────
step "Publish PhotoBooth.API..."
dotnet publish "$API_SRC" \
    -c "$CONFIG" \
    -r "$RUNTIME" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "$PUBLISH_OUT/api" \
    --nologo -v q
ok "API published"

# ── Bước 3: Publish PhotoBooth.Admin ─────────────────────────────────────────
step "Publish PhotoBooth.Admin..."
dotnet publish "$ADMIN_SRC" \
    -c "$CONFIG" \
    -r "$RUNTIME" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "$PUBLISH_OUT/admin" \
    --nologo -v q
ok "Admin published"

# ── Bước 4: Publish PhotoBooth.UI ────────────────────────────────────────────
step "Publish PhotoBooth.UI..."
dotnet publish "$UI_SRC" \
    -c "$CONFIG" \
    -r "$RUNTIME" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "$PUBLISH_OUT/ui" \
    --nologo -v q
ok "UI published"

# ── Bước 4.1: Publish PhotoBooth.Event ────────────────────────────────────────
step "Publish PhotoBooth.Event..."
dotnet publish "$EVENT_SRC" \
    -c "$CONFIG" \
    -r "$RUNTIME" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "$PUBLISH_OUT/event" \
    --nologo -v q
ok "Event published"

# ── Hàm Hỗ Trợ ────────────────────────────────────────────────────────────────
create_sub_app_bundle() {
    local src_out="$1"
    local dest_macos="$2"
    local bundle_name="$3"
    local display_name="$4"
    local bundle_id="$5"
    local executable="$6"

    local contents_dir="$(dirname "$dest_macos")"
    
    mkdir -p "$dest_macos"
    mkdir -p "$contents_dir/Resources"
    
    cp -R "$src_out/." "$dest_macos/"
    
    cat > "$contents_dir/Info.plist" << EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>$bundle_name</string>
    <key>CFBundleDisplayName</key>
    <string>$display_name</string>
    <key>CFBundleIdentifier</key>
    <string>$bundle_id</string>
    <key>CFBundleVersion</key>
    <string>1.0.0</string>
    <key>CFBundleShortVersionString</key>
    <string>1.0</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleExecutable</key>
    <string>$executable</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>NSCameraUsageDescription</key>
    <string>PhotoBooth cần truy cập camera để chụp ảnh</string>
    <key>LSMinimumSystemVersion</key>
    <string>12.0</string>
    <key>LSUIElement</key>
    <false/>
    <key>NSPrincipalClass</key>
    <string>NSApplication</string>
</dict>
</plist>
EOF
    chmod +x "$dest_macos/$executable"
    ok "$bundle_name bundle created with camera permission"
}

# ── Bước 5: Tạo .app bundle structure ────────────────────────────────────────
step "Tạo cấu trúc .app bundle..."
MACOS_DIR="$APP_BUNDLE/Contents/MacOS"
RESOURCES_DIR="$APP_BUNDLE/Contents/Resources"

mkdir -p "$MACOS_DIR/api"
mkdir -p "$RESOURCES_DIR"

# ── Bước 6: Copy binaries ─────────────────────────────────────────────────────
step "Copy binaries vào bundle..."

# Admin binary (entry point)
cp -R "$PUBLISH_OUT/admin/." "$MACOS_DIR/"

# API binary + assets
cp -R "$PUBLISH_OUT/api/." "$MACOS_DIR/api/"

# UI — đóng gói thành .app riêng để macOS cấp quyền camera
UI_APP_MACOS="$MACOS_DIR/ui/PhotoBooth.UI.app/Contents/MacOS"
create_sub_app_bundle "$PUBLISH_OUT/ui" "$UI_APP_MACOS" "PhotoBoothUI" "PhotoBooth" "com.photobooth.ui" "PhotoBooth.UI"

# Event — đóng gói thành .app riêng để macOS cấp quyền camera
EVENT_APP_MACOS="$MACOS_DIR/event/PhotoBooth.Event.app/Contents/MacOS"
create_sub_app_bundle "$PUBLISH_OUT/event" "$EVENT_APP_MACOS" "PhotoBoothEvent" "PhotoBooth Event" "com.photobooth.event" "PhotoBooth.Event"

# ── Bước 7: Copy extra assets ────────────────────────────────────────────────
step "Copy assets và dữ liệu..."

# Copy database nếu tồn tại (giữ data hiện có)
DB_SRC="$API_SRC/photobooth.db"
DB_DEST="$MACOS_DIR/api/photobooth.db"
if [ -f "$DB_SRC" ] && [ ! -f "$DB_DEST" ]; then
    cp "$DB_SRC" "$DB_DEST"
    ok "Database copied"
elif [ -f "$DB_SRC" ]; then
    ok "Database already in publish output"
fi

# Copy uploads folder nếu có
if [ -d "$API_SRC/uploads" ]; then
    cp -R "$API_SRC/uploads" "$MACOS_DIR/api/"
    ok "Uploads folder copied"
fi

# Copy wwwroot nếu có
if [ -d "$API_SRC/wwwroot" ]; then
    cp -R "$API_SRC/wwwroot" "$MACOS_DIR/api/"
    ok "wwwroot copied"
fi

# Copy UI Assets
if [ -d "$UI_SRC/Assets/finish" ]; then
    mkdir -p "$MACOS_DIR/api/ui_assets"
    cp -R "$UI_SRC/Assets/finish" "$MACOS_DIR/api/ui_assets/"
    ok "UI finish assets copied"
fi

# Copy Event Assets
if [ -d "$EVENT_SRC/Assets/finish" ]; then
    mkdir -p "$MACOS_DIR/api/event_assets"
    cp -R "$EVENT_SRC/Assets/finish" "$MACOS_DIR/api/event_assets/"
    ok "Event finish assets copied"
fi

# ── Bước 8: Tạo Info.plist ───────────────────────────────────────────────────
step "Tạo Info.plist..."
cat > "$APP_BUNDLE/Contents/Info.plist" << 'EOF'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>PhotoBoothAdmin</string>
    <key>CFBundleDisplayName</key>
    <string>PhotoBooth Admin</string>
    <key>CFBundleIdentifier</key>
    <string>com.photobooth.admin</string>
    <key>CFBundleVersion</key>
    <string>1.0.0</string>
    <key>CFBundleShortVersionString</key>
    <string>1.0</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleSignature</key>
    <string>????</string>
    <key>CFBundleExecutable</key>
    <string>PhotoBooth.Admin</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>NSCameraUsageDescription</key>
    <string>PhotoBooth cần truy cập camera để chụp ảnh</string>
    <key>LSMinimumSystemVersion</key>
    <string>12.0</string>
    <key>LSApplicationCategoryType</key>
    <string>public.app-category.photography</string>
    <key>NSPrincipalClass</key>
    <string>NSApplication</string>
</dict>
</plist>
EOF
ok "Info.plist created"

# ── Bước 9: Tạo settings.json mặc định ───────────────────────────────────────
step "Tạo settings.json mặc định..."
SETTINGS_FILE="$MACOS_DIR/settings.json"
if [ ! -f "$SETTINGS_FILE" ]; then
cat > "$SETTINGS_FILE" << 'EOF'
{
  "GoogleDriveEnabled": false,
  "GoogleDrivePath": "",
  "AppsScriptUrl": "",
  "EnablePrinting": false,
  "PrinterName": ""
}
EOF
    ok "settings.json created"
fi

# ── Bước 10: Set permissions ──────────────────────────────────────────────────
step "Set permissions..."

# Admin entry binary
ADMIN_BIN="$MACOS_DIR/PhotoBooth.Admin"
if [ -f "$ADMIN_BIN" ]; then
    chmod +x "$ADMIN_BIN"
    ok "Admin binary: executable"
fi

# API binary
API_BIN="$MACOS_DIR/api/PhotoBooth.API"
if [ -f "$API_BIN" ]; then
    chmod +x "$API_BIN"
    ok "API binary: executable"
fi

# Note: UI and Event binaries were already granted execution permissions during sub_app_bundle creation

# All other executables in the bundle
find "$APP_BUNDLE" -type f -perm +111 -exec chmod +x {} \; 2>/dev/null || true

# ── Bước 11: Remove macOS quarantine (nếu có) ────────────────────────────────
step "Remove quarantine attribute..."
xattr -rd com.apple.quarantine "$APP_BUNDLE" 2>/dev/null || true
ok "Quarantine cleared"

# ── Xong! ─────────────────────────────────────────────────────────────────────
echo ""
echo "╔══════════════════════════════════════════════════════╗"
echo "║                    BUILD HOÀN TẤT!                  ║"
echo "╚══════════════════════════════════════════════════════╝"
echo ""
echo -e "${GREEN}📦 Bundle:${NC} $APP_BUNDLE"
echo -e "${GREEN}📊 Kích thước:${NC} $(du -sh "$APP_BUNDLE" | cut -f1)"
echo ""
echo "Cách mở:"
echo "  1. Double-click PhotoBoothAdmin.app trong Finder"
echo "  2. Hoặc: open \"$APP_BUNDLE\""
echo ""
echo "Lần đầu mở có thể bị macOS chặn (Gatekeeper)."
echo "Cách bypass: Chuột phải → Open → Open"
echo ""
warn "Tài khoản mặc định: admin / admin123"

# ── Tự động cài vào /Applications ────────────────────────────────────────────
step "Cài vào /Applications..."
rm -rf "/Applications/$APP_NAME.app"
cp -R "$APP_BUNDLE" "/Applications/$APP_NAME.app"
ok "Đã cài: /Applications/$APP_NAME.app"
echo ""
echo "Mở từ Launchpad hoặc: open \"/Applications/$APP_NAME.app\""

