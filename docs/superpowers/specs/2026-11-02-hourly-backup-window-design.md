# Hourly Backup Window Design

## Intent

Run the existing Access database copy on aligned hourly times during a configurable daily window. The service must preserve the currently working shadow-copy behavior while avoiding damage to unrelated Windows snapshots and avoiding a missing or partially written destination file.

## Goals

- Read `StartTime`, `EndTime`, and `IntervalMinutes` from `appsettings.json`.
- With `StartTime` `10:00`, `EndTime` `21:00`, and `IntervalMinutes` `60`, run at 10:00, 11:00, and each aligned hour through 21:00 inclusive.
- If the service starts between slots, wait for the next aligned slot. If it starts after the final slot, wait for the next day's first slot.
- Never overlap backups. If a backup crosses a later slot, skip that missed slot and calculate the next future slot.
- Preserve the existing MDB copy result and configurable source/destination paths.
- Delete only the VSS snapshot created by this worker, including after failure.
- Replace the destination only after a complete temporary copy has been validated.
- Validate configuration at startup and make command execution cancellable, bounded by a timeout, and sensitive to non-zero exit codes.

## Non-goals

- No UI, remote administration, cloud storage, or multi-machine scheduling.
- No change to OfficeBackend or OfficeUI.
- No historical retention policy beyond safely replacing the configured destination file.

## Constraints

- Continue as a .NET 8 Windows Worker Service.
- Continue using the VSS-based approach because it already copies the live Access database successfully on the target machine.
- Preserve the user's uncommitted deployment paths in `appsettings.json` while adding the required schedule settings.
- The destination filename remains configurable, defaulting to `att2026.mdb` for compatibility.

## Approaches Considered

1. Relative interval from service startup: simple, but runs at arbitrary times such as 10:37 and does not match the requested clock schedule.
2. Aligned configurable window: selected. It produces predictable clock times and a strict inclusive end time.
3. Windows Task Scheduler: operationally viable, but moves scheduling outside this existing Windows service and complicates deployment ownership.

## Error and Safety Behavior

- A failed backup is logged and retried at the next configured aligned slot; it does not run outside the configured window.
- Cancellation stops waiting and command execution without being logged as a backup failure.
- The exact created Shadow ID is captured, used to locate the shadow device, and deleted in `finally`; the worker never issues a global shadow-copy deletion.
- Copy to a temporary file in the destination directory, confirm it exists and is non-empty, then atomically replace the final file. A failure keeps the previous final backup intact.

## Verification

- Unit tests cover schedule boundaries, startup between slots, inclusive end time, overnight wait, invalid options, missed slots, exact-snapshot cleanup, command failure/timeout, and atomic replacement behavior.
- Run the full test suite, Release build, and package vulnerability audit.
