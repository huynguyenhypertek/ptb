import AVFoundation

# Get all video devices
devices = AVFoundation.AVCaptureDevice.devicesWithMediaType_(AVFoundation.AVMediaTypeVideo)

if not devices:
    print("No cameras found by AVFoundation!")
else:
    print(f"AVFoundation found {len(devices)} cameras:")
    for i, device in enumerate(devices):
        print(f"Index {i}: {device.localizedName()}")
