# Archiver

**Scheduled file archiving to network shares and external drives — runs as a Windows Service.**

Archiver copies files from configurable source folders to network shares (and optionally local/USB drives) on a schedule. It can also trim source folders by age to free up disk space. A WPF desktop app provides graphical management of jobs, logs, and the service.


---

## Features

- **Windows Service** — runs in the background, starts automatically on boot
- **Multiple copy jobs** — each with its own source folder, destinations, schedule, and cleanup policy
- **Network shares** — copies to UNC paths with support for password-protected shares (DPAPI-encrypted credentials)
- **Local/USB drives** — optional secondary destination per job for offline backups
- **Flexible scheduling** — daily wall-clock times, interval (every N minutes), or cron expressions
- **Source trimming** — optionally delete files from the source after they exceed a retention age, keeping your primary drives clean
- **Resilient copy** — exponential-backoff retry for transient network/IO errors, file-lock handling, size verification
- **WPF management UI** — add/edit jobs, view live service status, browse searchable logs with level/date filters
- **Hot-reload** — edit `config.json` directly and the service picks up changes without restarting
- **Structured logging** — daily rolling JSON log files in `C:\ProgramData\Archiver\logs\`

## Quick Start

### Prerequisites

- Windows 10+ (x64)
- [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)

### Build & Publish

```powershell
dotnet restore
dotnet build Archiver.slnx
dotnet publish Archiver.slnx -c Release -r win-x64 -p:PublishSingleFile=true -o .\publish
```

### Install the Service

```powershell
# Run as Administrator
powershell -File .\install\Install.ps1
```

The service installs to `C:\ProgramData\Archiver\Service\` with a default example config at `C:\ProgramData\Archiver\config.json`. Logs go to `C:\ProgramData\Archiver\logs\`.

### Uninstall

```powershell
# Keeps config and logs
powershell -File .\install\Uninstall.ps1

# Removes everything
powershell -File .\install\Uninstall.ps1 -RemoveData
```

## Configuration

Edit `C:\ProgramData\Archiver\config.json` (or use the WPF UI for a graphical editor).

### Minimal Example

```json
{
  "service": {
    "logRetentionDays": 30,
    "copyRetryCount": 3,
    "copyRetryDelaySeconds": 10,
    "simultaneousJobCount": 1
  },
  "jobs": [
    {
      "name": "NightlyBackups",
      "enabled": true,
      "sourcePath": "C:\\SQLBackups",
      "includeSubdirectories": false,
      "fileFilter": "*.bak;*.trn",
      "destinationNetworkPath": "\\\\corp-nas\\archives\\SQLBackups",
      "destinationLocalPath": null,
      "schedule": {
        "type": "Daily",
        "times": ["02:00"]
      },
      "cleanup": {
        "enabled": true,
        "retentionDays": 30,
        "minFreeDiskPercent": 5
      }
    }
  ]
}
```

### Job Fields

| Field | Description |
|---|---|
| `name` | Unique job name |
| `enabled` | `true` / `false` |
| `sourcePath` | Absolute path to source folder |
| `includeSubdirectories` | Recurse into subfolders |
| `fileFilter` | Semicolon-separated patterns (`"*.bak;*.zip"`). `null` = all files |
| `destinationNetworkPath` | UNC path (`\\server\share`). Optional if `destinationLocalPath` is set |
| `destinationLocalPath` | Local/USB drive path. Optional |
| `credentialName` | Name of a credential entry in `credentials[]`. Omit to use machine account |
| `schedule.type` | `Daily`, `Interval`, or `Cron` |
| `schedule.times` | `["HH:mm", ...]` for `Daily` |
| `schedule.intervalMinutes` | Integer for `Interval` |
| `schedule.cronExpression` | Cron string for `Cron` (e.g. `"0 */6 * * *"`) |
| `cleanup.enabled` | Whether to trim source files after copy |
| `cleanup.retentionDays` | Delete source files older than this many days |
| `cleanup.minFreeDiskPercent` | Stop cleanup if free space falls below this threshold |
| `cleanup.removeEmptyDirectories` | Delete empty subdirs after cleanup |

### Credentials

For password-protected network shares, add entries under `credentials[]`:

```json
"credentials": [
  {
    "name": "NAS-Creds",
    "domain": "CORP",
    "username": "svc_archiver",
    "passwordEncrypted": "<DPAPI ciphertext — use the WPF UI to set this>",
    "targetShares": ["\\\\corp-nas\\archives"],
    "method": "Win32"
  }
]
```

- `passwordEncrypted` — Windows DPAPI ciphertext. **Use the WPF UI** to enter passwords; they are encrypted under your user account before writing to disk.
- `method` — `Win32` (WNetAddConnection2 API, default) or `NetUse` (shells out to `net use`)
- `targetShares` — UNC paths this credential applies to. Used for auto-matching when `credentialName` isn't set on a job.

If no credentials are configured for a share, the service falls back to the machine account (works in Active Directory environments where the computer object has share permissions).

## WPF Management UI

Launch `C:\ProgramData\Archiver\Service\Archiver.UI.exe` (or from the publish folder during development).

The UI has three pages:

- **Service Status** — start/stop the service, view recent activity
- **Copy Jobs** — list all jobs with last-run status, double-click to edit, add new jobs with configuration wizard
- **Logs** — browse structured logs with date/level/text filtering

**Note:** Starting/stopping the service requires Administrator elevation. Viewing status and editing config works without elevation.

## Architecture

```
Archiver.slnx
├── src/
│   ├── Archiver.Core/        Class Library (business logic, config models, services)
│   ├── Archiver.Service/     Windows Service (BackgroundService host, job scheduler)
│   └── Archiver.UI/          WPF Application (management dashboard)
└── install/
    ├── Install.ps1
    └── Uninstall.ps1
```

### Key Design Decisions

| Area | Approach |
|---|---|
| **Retry** | Exponential backoff (10s → 20s → 40s) for transient IO/network/socket errors |
| **File locks** | Up to 6 attempts with incremental delay before skipping a locked file |
| **Copy verification** | File size comparison after write; automatic re-copy on mismatch |
| **Config hot-reload** | `FileSystemWatcher` detects external changes, service re-schedules without restart |
| **Credentials** | Windows DPAPI (`ProtectedData`) — only the encrypting account can decrypt |
| **Network auth** | `WNetAddConnection2` P/Invoke with `net use` fallback |
| **Scheduling** | `Cronos` library for cron parsing; custom logic for Daily/Interval |
| **IPC** | File-based: `status.json` for job run results, `.trigger` files for "Run Now" |
| **Logging** | Rolling daily structured JSON files + Windows Event Log for critical errors |

## Service Management

```powershell
# Start / Stop / Restart
sc.exe start Archiver
sc.exe stop Archiver

# Or from services.msc
services.msc

# View logs
Get-Content C:\ProgramData\Archiver\logs\archiver-2026-06-12.log
```

## Troubleshooting

| Symptom | Check |
|---|---|
| Service won't start | `C:\ProgramData\Archiver\logs\` for startup errors; config.json for syntax issues |
| "Source path not found" | Verify the path exists and the service account has read access |
| Network copy fails | Test credentials via WPF UI's "Test Connection" button; verify SMB access from the machine |
| Files not being cleaned up | `cleanup.enabled` must be `true`; check `retentionDays` and file `LastWriteTime` |
| WPF UI can't start/stop service | Must be run **as Administrator** |

## License

[MIT](LICENSE)
