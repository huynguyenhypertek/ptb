# Story 5.3: Build Package Script for Event Mode

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an Admin/Developer,
I want to include PhotoBooth.Event in the build script,
so that the CI/CD or local build can package both UI and Event applications into the single Admin launcher bundle.

## Acceptance Criteria

1. [x] `build-mac-app.sh` is updated to publish `PhotoBooth.Event` using the same configuration (`$CONFIG`, `$RUNTIME`, self-contained) as `PhotoBooth.UI`.
2. [x] `PhotoBooth.Event` is packaged as a standalone `.app` bundle inside the main `PhotoBoothAdmin.app` (at `Contents/MacOS/event/PhotoBooth.Event.app`).
3. [x] `PhotoBooth.Event` has its own `Info.plist` with unique bundle identifier (`com.photobooth.event`) and camera permissions (`NSCameraUsageDescription`).
4. [x] `PhotoBooth.Event` executable is correctly granted executable permissions (`chmod +x`).
5. [x] Existing `PhotoBooth.UI`, `PhotoBooth.API`, and `PhotoBooth.Admin` build processes remain intact.

## Tasks / Subtasks

- [x] Task 1: Update source variables and publish step (AC: 1, 5)
  - [x] Add `EVENT_SRC="$SRC/PhotoBooth.Event"` alongside other `_SRC` variables.
  - [x] Duplicate the "Publish PhotoBooth.UI" step, adapting it for `PhotoBooth.Event`. Output to `$PUBLISH_OUT/event`.
- [x] Task 2: Create bundle structure and copy binaries (AC: 2)
  - [x] Under "Tạo cấu trúc .app bundle", add `mkdir -p "$MACOS_DIR/event/PhotoBooth.Event.app/Contents/MacOS"` and `Resources`.
  - [x] Under "Copy binaries", define `EVENT_APP_MACOS="$MACOS_DIR/event/PhotoBooth.Event.app/Contents/MacOS"`.
  - [x] Copy from `$PUBLISH_OUT/event/.` to `$EVENT_APP_MACOS/`.
- [x] Task 3: Configure Info.plist and Permissions (AC: 3, 4)
  - [x] Generate `Info.plist` for `PhotoBooth.Event.app` using `cat >`. 
    - **CRITICAL**: Change `CFBundleIdentifier` to `com.photobooth.event`.
    - **CRITICAL**: Change `CFBundleName` to `PhotoBoothEvent`.
    - **CRITICAL**: Change `CFBundleExecutable` to `PhotoBooth.Event`.
    - **CRITICAL**: Keep `NSCameraUsageDescription` as camera access is required.
  - [x] Under "Set permissions", add `chmod +x` for `$MACOS_DIR/event/PhotoBooth.Event.app/Contents/MacOS/PhotoBooth.Event`.

## Dev Notes

### Technical Requirements
- **Build Flags**: Ensure `dotnet publish` uses `-c "$CONFIG" -r "$RUNTIME" --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true`.
- **macOS Permissions**: macOS requires camera-using apps to be packaged as `.app` bundles with an `Info.plist`. The script already does this for UI; you are replicating this pattern for Event.
- **Bundle ID Collision**: Do not reuse `com.photobooth.ui` for the Event app; use `com.photobooth.event` to prevent macOS TCC (Transparency, Consent, and Control) permission overrides.

### Project Structure Notes
- The resulting structure within the main `.app` must be:
  `PhotoBoothAdmin.app/Contents/MacOS/event/PhotoBooth.Event.app/Contents/MacOS/PhotoBooth.Event`
- This ensures `DeviceLauncherViewModel` (modified in E5-S2) can reliably locate and execute the event app.

### References
- Source: `_bmad-output/sprint-status.yaml`
- Source: `build-mac-app.sh`

## Dev Agent Record

### Agent Model Used

Gemini 3.1 Pro (High)

### Debug Log References

### Completion Notes List
- Updated `build-mac-app.sh` to publish `PhotoBooth.Event`.
- **[AI-Review Fix]**: Refactored `build-mac-app.sh` to use `create_sub_app_bundle` to eliminate DRY violations.
- **[AI-Review Fix]**: Removed redundant permissions checks in `build-mac-app.sh`.
- **[AI-Review Fix]**: Added `EVENT_SRC/Assets/finish` copying logic.
- **[AI-Review Fix]**: Added `build-mac-app.sh` to git staging.

### File List
- `build-mac-app.sh`
