# Hourly Backup Window Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Run the Access database backup on aligned configurable hourly slots between an inclusive configurable start and end time, with safe VSS ownership and atomic destination replacement.

**Architecture:** Bind and validate typed backup options at startup. A pure schedule component calculates the next aligned slot; a worker waits for it and invokes a backup executor. The executor owns one exact VSS snapshot and delegates copying to an atomic file copier, while a bounded asynchronous process runner uses supported PowerShell CIM commands.

**Tech Stack:** .NET 8 Worker Service, Microsoft.Extensions.Options, System.Diagnostics.Process, xUnit

**Spec:** `docs/superpowers/specs/2026-11-02-hourly-backup-window-design.md`

## Global Constraints

- Continue as a .NET 8 Windows Worker Service.
- Continue using the working VSS-based copy approach.
- Preserve the user's uncommitted `SourceFile` and `BackupFolder` values.
- Defaults: `StartTime=10:00`, `EndTime=21:00`, `IntervalMinutes=60`, `BackupFileName=att2026.mdb`, `CommandTimeoutSeconds=120`.
- The end time is inclusive and backups never overlap.
- Never delete a VSS snapshot not created by this worker.
- Never remove the previous destination until a complete non-empty temporary copy exists.

## Review Focus

- A service start exactly at `EndTime` must schedule that final slot, while one tick later must schedule the next day.
- An interval that does not divide the window evenly must never schedule after `EndTime`.
- A malformed CIM response after snapshot creation must still clean up the parsed Shadow ID.
- Cancellation or timeout must terminate the child process tree and must not be reported as a normal successful command.
- A failed replacement must retain the previous destination and remove the temporary file.

---

### Task 1: Typed configuration and aligned schedule

**Files:**
- Create: `AUTORESTART1/Configuration/BackupOptions.cs`
- Create: `AUTORESTART1/Configuration/BackupOptionsValidator.cs`
- Create: `AUTORESTART1/Scheduling/HourlyBackupSchedule.cs`
- Create: `AUTORESTART1.Tests/AUTORESTART1.Tests.csproj`
- Create: `AUTORESTART1.Tests/Scheduling/HourlyBackupScheduleTests.cs`
- Create: `AUTORESTART1.Tests/Configuration/BackupOptionsValidatorTests.cs`
- Modify: `AUTORESTART1.sln`

**Interfaces:**
- Produces: `BackupOptions` with `SourceFile`, `BackupFolder`, `BackupFileName`, `StartTime`, `EndTime`, `IntervalMinutes`, `CommandTimeoutSeconds`.
- Produces: `BackupOptionsValidator.Validate(string?, BackupOptions) -> ValidateOptionsResult`.
- Produces: `HourlyBackupSchedule.GetNextRun(DateTime now) -> DateTime`.

- [ ] Write failing schedule tests for before-window, between-slot, exact-slot, exact inclusive end, after-end, a non-dividing interval, and a missed slot after a long backup.
- [ ] Write failing validator tests for missing paths, unsafe filename, start after end, non-positive interval, and non-positive timeout.
- [ ] Run `dotnet test AUTORESTART1.Tests/AUTORESTART1.Tests.csproj` and verify compilation/test failure because production types do not exist.
- [ ] Implement the options, validator, and schedule with aligned slots originating at `StartTime` and never exceeding `EndTime`.
- [ ] Run the task tests and verify they pass.

### Task 2: Safe process, exact snapshot ownership, and atomic copy

**Files:**
- Create: `AUTORESTART1/Backup/ICommandRunner.cs`
- Create: `AUTORESTART1/Backup/ProcessCommandRunner.cs`
- Create: `AUTORESTART1/Backup/IShadowCopyService.cs`
- Create: `AUTORESTART1/Backup/CimShadowCopyService.cs`
- Create: `AUTORESTART1/Backup/IBackupFileCopier.cs`
- Create: `AUTORESTART1/Backup/AtomicBackupFileCopier.cs`
- Create: `AUTORESTART1/Backup/BackupExecutor.cs`
- Create: `AUTORESTART1.Tests/Backup/CimShadowCopyServiceTests.cs`
- Create: `AUTORESTART1.Tests/Backup/AtomicBackupFileCopierTests.cs`
- Create: `AUTORESTART1.Tests/Backup/BackupExecutorTests.cs`

**Interfaces:**
- Consumes: `BackupOptions` from Task 1.
- Produces: `ICommandRunner.RunAsync(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken) -> Task<CommandResult>`.
- Produces: `IShadowCopyService.CreateAsync(string sourceFile, CancellationToken) -> Task<ShadowCopy>` and `DeleteAsync(ShadowCopy, CancellationToken) -> Task`.
- Produces: `IBackupFileCopier.CopyAsync(string sourceFile, string destinationFile, CancellationToken) -> Task`.
- Produces: `BackupExecutor.ExecuteAsync(CancellationToken) -> Task`.

- [ ] Write failing CIM tests proving the created Shadow ID selects the device path, non-zero CIM return values fail, malformed post-create output cleans up the exact ID, and no global deletion command is emitted.
- [ ] Write failing copier tests proving successful replacement, non-empty validation, old-file preservation on validation failure, and temporary-file cleanup.
- [ ] Write failing executor tests proving copy from the snapshot-relative source and exact-snapshot cleanup after success and copy failure.
- [ ] Run the backup tests and verify they fail because production types do not exist.
- [ ] Implement the cancellable process runner with concurrent output reads, timeout, process-tree termination, and non-zero exit-code rejection.
- [ ] Implement exact-ID CIM snapshot creation/query/deletion and ensure create-time partial failures delete the parsed ID.
- [ ] Implement async temporary copying and same-directory atomic replacement while retaining the old file until replacement.
- [ ] Implement `BackupExecutor` with snapshot deletion in `finally`.
- [ ] Run the task tests and verify they pass.

### Task 3: Worker integration and deployment configuration

**Files:**
- Replace: `AUTORESTART1/Worker.cs`
- Modify: `AUTORESTART1/Program.cs`
- Modify: `AUTORESTART1/appsettings.json`
- Create: `AUTORESTART1.Tests/WorkerSchedulingTests.cs`

**Interfaces:**
- Consumes: `BackupOptions`, `HourlyBackupSchedule`, and `BackupExecutor` from Tasks 1 and 2.
- Produces: `BackupWorker` that waits until each aligned slot, runs one backup, skips elapsed slots, and stops cleanly on cancellation.

- [ ] Write a failing worker scheduling test with a controllable `TimeProvider` proving the worker waits for an aligned slot and invokes one backup without overlap.
- [ ] Run the worker test and verify it fails against the current five-minute implementation.
- [ ] Replace the diagnostic-heavy worker with the typed schedule loop and clean cancellation/error logging.
- [ ] Register validated options and all services in `Program.cs`.
- [ ] Preserve current local paths and replace ignored `RunTime` with `StartTime`, `EndTime`, `IntervalMinutes`, `BackupFileName`, and `CommandTimeoutSeconds`.
- [ ] Run the complete test suite and verify all tests pass.
- [ ] Run `dotnet build AUTORESTART1.sln -c Release` and verify zero errors.
- [ ] Run `dotnet list AUTORESTART1/AUTORESTART1.csproj package --vulnerable --include-transitive` and report the result.

## Self-review

- Spec coverage: scheduling, strict window, safe snapshot cleanup, atomic copy, validation, timeout, and verification are each assigned to a task.
- Type consistency: Task 3 consumes the exact option, schedule, and executor interfaces produced by Tasks 1 and 2.
- Review-focus coverage: each listed boundary or failure mode has an owning test step.
- Proportion: implementation details are limited to signatures, fixed values, and safety rules; method bodies remain implementation work.
