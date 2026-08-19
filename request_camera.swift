import AVFoundation
import Foundation

print("Đang yêu cầu quyền Camera, vui lòng kiểm tra trên màn hình...")
AVCaptureDevice.requestAccess(for: .video) { granted in
    print("Quyền Camera: \(granted ? "ĐÃ CẤP ✅" : "TỪ CHỐI ❌")")
    exit(0)
}

RunLoop.main.run()
