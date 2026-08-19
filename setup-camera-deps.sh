#!/bin/bash
# ============================================================
# setup-camera-deps.sh
# Tự động cài Homebrew + libavif để fix lỗi OpenCvSharp
# ============================================================

set -e  # Dừng nếu có lỗi

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo ""
echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}  PhotoBooth Camera Setup - macOS       ${NC}"
echo -e "${BLUE}========================================${NC}"
echo ""

# ---- Bước 1: Cài Homebrew nếu chưa có ----
if command -v brew &>/dev/null; then
    echo -e "${GREEN}✅ Homebrew đã được cài rồi.${NC}"
else
    echo -e "${YELLOW}📦 Đang cài Homebrew...${NC}"
    /bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"

    # Thêm brew vào PATH cho Intel Mac
    if [[ -f "/usr/local/bin/brew" ]]; then
        eval "$(/usr/local/bin/brew shellenv)"
    fi
    # Thêm brew vào PATH cho Apple Silicon Mac
    if [[ -f "/opt/homebrew/bin/brew" ]]; then
        eval "$(/opt/homebrew/bin/brew shellenv)"
    fi

    echo -e "${GREEN}✅ Homebrew đã được cài thành công!${NC}"
fi

echo ""

# ---- Bước 2: Cài libavif ----
if brew list libavif &>/dev/null 2>&1; then
    echo -e "${GREEN}✅ libavif đã được cài rồi.${NC}"
else
    echo -e "${YELLOW}📦 Đang cài libavif (và các dependency: libdav1d, libaom)...${NC}"
    brew install libavif
    echo -e "${GREEN}✅ libavif đã được cài thành công!${NC}"
fi

echo ""

# ---- Bước 3: Kiểm tra file dylib ----
echo -e "${BLUE}🔍 Kiểm tra file thư viện...${NC}"

# Xác định prefix của Homebrew
BREW_PREFIX=$(brew --prefix)
LIBAVIF_PATH="$BREW_PREFIX/opt/libavif/lib/libavif.16.dylib"

if [[ -f "$LIBAVIF_PATH" ]]; then
    echo -e "${GREEN}✅ Tìm thấy: $LIBAVIF_PATH${NC}"
else
    # Thử tìm ở path khác
    LIBAVIF_PATH=$(find "$BREW_PREFIX" -name "libavif.16.dylib" 2>/dev/null | head -1)
    if [[ -n "$LIBAVIF_PATH" ]]; then
        echo -e "${GREEN}✅ Tìm thấy: $LIBAVIF_PATH${NC}"
    else
        echo -e "${RED}❌ Không tìm thấy libavif.16.dylib! Thử cài lại:${NC}"
        echo "   brew reinstall libavif"
        exit 1
    fi
fi

# ---- Bước 4: Tạo symlink nếu cần (cho Intel Mac với path /usr/local/opt) ----
EXPECTED_PATH="/usr/local/opt/libavif/lib/libavif.16.dylib"
if [[ ! -f "$EXPECTED_PATH" && -f "$LIBAVIF_PATH" ]]; then
    echo ""
    echo -e "${YELLOW}🔗 Tạo symlink tới đường dẫn chuẩn...${NC}"
    sudo mkdir -p /usr/local/opt/libavif/lib
    sudo ln -sf "$LIBAVIF_PATH" "$EXPECTED_PATH"
    echo -e "${GREEN}✅ Symlink tạo thành công: $EXPECTED_PATH${NC}"
fi

echo ""
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}  ✅ Cài đặt hoàn tất!                  ${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""
echo -e "${BLUE}Chạy ứng dụng PhotoBooth:${NC}"
echo "  dotnet run --project src/PhotoBooth.Event -- --deviceId=Pb1_Ch1 --storeId=1 --apiBaseUrl=http://localhost:5148"
echo ""
