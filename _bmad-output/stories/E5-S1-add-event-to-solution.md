# Story 5.1: add-event-to-solution

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer,
I want to add the PhotoBooth.Event project to the PhotoBooth solution file,
so that it can be built and managed alongside the rest of the application.

## Acceptance Criteria

1. `PhotoBooth.Event` is properly included in `PhotoBooth.slnx`.
2. The solution builds successfully with all projects including `PhotoBooth.Event`.

## Tasks / Subtasks

- [x] Task 1: Verify PhotoBooth.slnx configuration
  - [x] Validate that `PhotoBooth.Event.csproj` exists under the `/src/` folder in `PhotoBooth.slnx`.
- [x] Task 2: Verify Build
  - [x] Run `dotnet build PhotoBooth.slnx` to ensure the entire solution compiles without errors.
- [x] Review Follow-ups (AI)
  - [x] [AI-Review][High] Add missing `PhotoBooth.API` project to `PhotoBooth.slnx`
  - [x] [AI-Review][Medium] Fix vulnerability in `Tmds.DBus.Protocol` by bumping version to `0.94.2`
  - [x] [AI-Review][Medium] Fix vulnerability in `SQLitePCLRaw.lib.e_sqlite3` by bumping version to `3.53.3`
  - [x] [AI-Review][Medium] Sort `PhotoBooth.slnx` `src` folder projects alphabetically

## Dev Notes

- **Note on Current State**: `PhotoBooth.Event.csproj` appears to have already been added to `PhotoBooth.slnx` in a previous step. The dev agent should simply verify this and make sure the build passes.
- **Pre-verification Note**: No new dependencies or structural changes are required for this story. This is strictly a build and configuration check.
- **Source tree components to touch**: None expected unless structural validation fails.
- **Testing standards summary**: Solution compilation is the primary validation.

### Project Structure Notes

- Alignment with unified project structure: The `PhotoBooth.Event` project has already been created (Epic 0). This task is just confirming its presence in the `.slnx` solution file for IDEs and unified build scripts to recognize.

### References

- From `sprint-status.yaml`: `E5-S1-add-event-to-solution` -> "reuse: write_new — thêm PhotoBooth.Event vào PhotoBooth.slnx"

## Dev Agent Record

### Agent Model Used

Gemini 3.1 Pro

### Debug Log References

### Completion Notes List

- Verified `PhotoBooth.Event.csproj` is correctly included under the `/src/` folder in `PhotoBooth.slnx`.
- Executed `dotnet build PhotoBooth.slnx` and confirmed that all projects in the solution compile successfully with 0 errors.
- Added missing API project to `PhotoBooth.slnx` and alphabetized.
- Mitigated DBus protocol CVE warnings by bumping transitive dependencies explicitly.

### File List

- `PhotoBooth.slnx`
- `src/PhotoBooth.UI/PhotoBooth.UI.csproj`
- `src/PhotoBooth.Event/PhotoBooth.Event.csproj`
- `tests/PhotoBooth.Tests/PhotoBooth.Tests.csproj`
