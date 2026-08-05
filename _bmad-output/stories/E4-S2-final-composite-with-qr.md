# Story 4.2: Final Composite With QR

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer,
I want to expand the `ImageCompositeService` to support overlaying a QR code onto the final composite image,
so that the final printed photo includes a QR code for users to download their digital copy.

## Acceptance Criteria

1. Extend `ImageCompositeService` in `PhotoBooth.Infrastructure` to include a new method: `public static string? OverlayQrCode(string imagePath, byte[] qrBytes)`.
2. The method must safely decode the `qrBytes` (PNG format from QRCoder) directly into OpenCV format using `Mat.FromImageData()` to avoid unnecessary Avalonia Bitmap conversions.
3. **Dynamic Scaling & Positioning:** Scale the QR code proportionally to approximately 10-15% of the target composite image's height. Place the QR code in the bottom-left corner with a 20px margin from both edges.
4. **Alpha Blending Constraint:** The QR code must retain a solid white background to ensure high contrast for scanners. Ensure it is not mistakenly made transparent in areas that need to be opaque. Because the QR is opaque, avoid complex alpha blending and simply define an OpenCV ROI (Region of Interest) on the target image and use `CopyTo()`.
5. The method must safely paste the QR code onto the target image without crashing or leaking memory (using proper `using` statements for `Mat`).
6. Like `OverlaySequentialNumber`, it should save a backup of the original image. Crucially, if writing the final image fails, it must restore the backup of the original image before throwing the exception to prevent corrupted files. Ensure OpenCV operations properly release/dispose `Mat` objects.

## Tasks / Subtasks

- [x] Task 1: Add QR Overlay capability to `ImageCompositeService`
  - [x] Implement `public static string? OverlayQrCode(string imagePath, byte[] qrBytes)` in `src/PhotoBooth.Infrastructure/Services/ImageCompositeService.cs`.
  - [x] Decode `qrBytes` into an OpenCV `Mat` using `Mat.FromImageData()`.
  - [x] Calculate the scale (10-15% of `imagePath` height) and resize the decoded QR `Mat`.
  - [x] Define position (bottom-left corner with 20px margin).
  - [x] Handle pasting using OpenCV's ROI capabilities and `CopyTo()` directly, avoiding complex per-pixel alpha blending to preserve the solid background for the QR code.
  - [x] Add error handling and fallback logic: save a backup image before modification, and if writing the final image fails, catch the error, restore the backup, and rethrow.
- [x] Task 2: Unit Testing
  - [x] Write or update unit tests for `ImageCompositeService` to ensure `OverlayQrCode` executes without errors when given valid PNG byte arrays. Mock a base image (e.g., 1000x1000) and verify that the output file is created successfully, no exceptions are thrown, and memory usage is stable.

## Dev Notes

- We are extending `ImageCompositeService` which is referenced from `PhotoBooth.Infrastructure`.
- Ensure we use proper `using` statements for all OpenCV `Mat` objects to avoid memory leaks.
- The actual generation of the QR code (using `QRCoder.PngByteQRCode`) and the ViewModel integration will happen in story `E4-S3-qr-generate-embed-image`. This story focuses purely on the OpenCV capability to overlay an image array onto another image.
- **Clarification**: We will NOT display the QR code on the screen/UI. It will be printed directly into the final photo.

### Project Structure Notes

- File to modify: `src/PhotoBooth.Infrastructure/Services/ImageCompositeService.cs`

### References

- Existing implementation of `OverlaySequentialNumber` in `ImageCompositeService.cs`.
- QR generation in `PhotoBooth.UI/ViewModels/ThankYouViewModel.cs` for context on how `qrBytes` are generated.

## Dev Agent Record

### Agent Model Used

Gemini 3.1 Pro (High)

### Debug Log References

- Tests passed successfully for QR overlay logic.

### Completion Notes List

- ✅ Implemented `OverlayQrCode` method in `ImageCompositeService.cs` using OpenCV.
- ✅ Successfully decoded QR bytes with `Mat.FromImageData()`.
- ✅ Handled dynamic scaling (set to 12% of image height, minimum 50px) and bottom-left positioning (20px margins).
- ✅ Used ROI-based direct `CopyTo()` to preserve white QR background (no alpha blending).
- ✅ Added error handling and original image backup mechanism.
- ✅ Added 6 new unit tests in `ImageCompositeServiceOverlayTests.cs`.
- ✅ [AI-Review][High] Rewrote tests and added a memory stability test looping 50 times with a 1000x1000 image.
- ✅ [AI-Review][Medium] Changed QR resizing interpolation to `InterpolationFlags.Nearest` to preserve hard binary edges.
- ✅ [AI-Review][Low] Changed `OverlayQrCode` and `OverlaySequentialNumber` to delete the `_original.png` backup on success and return `bool`.
- ℹ️ [AI-Review] The git file list discrepancy was verified to be unrelated changes for other stories in progress.

### File List

- src/PhotoBooth.Infrastructure/Services/ImageCompositeService.cs
- tests/PhotoBooth.Tests/Services/ImageCompositeServiceOverlayTests.cs
