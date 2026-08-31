# Local development

## Prerequisites

- .NET SDK 10
- Node.js 24 and npm
- Git
- Internet access to `data.geo.admin.ch` for radar data
- Internet access to `tile.openstreetmap.org` for the browser base map

Azure credentials and a Storage Account are not required for local file-storage development.

## Install and verify dependencies

Run the commands from the repository root:

```bash
npm ci
npm run vendor
dotnet restore SwissRainRadar.slnx
dotnet build SwissRainRadar.slnx --configuration Release --no-restore
dotnet test SwissRainRadar.slnx --configuration Release --no-build
```

`npm ci` installs the exact Leaflet version recorded in `package-lock.json`. `npm run vendor` copies the browser-ready Leaflet files into the ASP.NET `wwwroot` directory. The generated files are ignored by Git.

## First local start

The default configuration enables a 14-day source-data backfill. Disable it for the first interactive test to reduce download time and disk use.

### PowerShell

```powershell
$env:Radar__BackfillOnStartup = "false"
dotnet run --project src/SwissRainRadar.Web
```

Remove the override for a later terminal session with:

```powershell
Remove-Item Env:Radar__BackfillOnStartup
```

### Bash

```bash
Radar__BackfillOnStartup=false \
  dotnet run --project src/SwissRainRadar.Web
```

Open the HTTP or HTTPS URL printed by ASP.NET. The exact development port is selected by the local .NET environment.

## Startup behavior

The application begins serving HTTP and starts `RadarUpdateWorker` in the same process. The worker checks MeteoSwiss immediately and then every five minutes. The first map is unavailable until enough source files have been downloaded and processed.

During this initial period:

- `/healthz` can already report a healthy web process.
- `/api/maps/latest` returns HTTP 503 until `latest.json` exists.
- The frontend displays its no-map or loading state.
- The application log shows download, incomplete-period or publication messages.

A successful publication logs a message similar to:

```text
Published 5 rain maps ending at 2026-08-31T12:00:00+00:00.
```

## Local storage

The default configuration contains an empty `Storage:AccountUri`, so `FileObjectStore` is selected. Files are stored beneath the configured `Storage:LocalRoot`, normally `App_Data` under the web project's content root:

```text
src/SwissRainRadar.Web/App_Data/
├── raw/yyyy/MM/dd/*.h5
└── maps/
    ├── history/yyyyMMddHHmm/*.png
    └── latest.json
```

Already present HDF5 files are reused on later updates and application restarts.

To choose another local directory, use an ASP.NET configuration override. For example, in PowerShell:

```powershell
$env:Storage__LocalRoot = "D:\SwissRainRadarData"
```

Do not set `Storage__AccountUri` unless the application should use Azure Blob Storage.

## Useful endpoints

Replace `<base-url>` with the URL shown by `dotnet run`.

| Endpoint | Expected behavior |
|---|---|
| `<base-url>/` | Loads the browser application |
| `<base-url>/healthz` | Returns a JSON health response |
| `<base-url>/api/maps/latest` | Returns the current manifest or HTTP 503 |
| `<base-url>/api/maps/<yyyyMMddHHmm>/<hours>` | Returns a PNG for a supported period |

Example:

```bash
curl http://localhost:5000/healthz
curl -i http://localhost:5000/api/maps/latest
```

## Manual verification checklist

After the first map is published, verify:

- The rain overlay is geographically aligned with Switzerland.
- The image is not mirrored or vertically inverted.
- Dry cells remain transparent.
- The 1, 3, 6, 12 and 24-hour selectors load their corresponding maps.
- The map keeps its zoom and center when the period changes.
- Mouse, touch zoom and panning work.
- The displayed source time agrees with the manifest.
- A second update does not download an already stored HDF5 asset again.
- A temporary MeteoSwiss failure is logged while the previous map remains usable.

## Testing local Azure Blob Storage behavior

Local development normally uses `FileObjectStore`. To exercise `BlobObjectStore`, provide a real Azure Storage Account URI and credentials supported by `DefaultAzureCredential`, for example an `az login` identity with Blob data permissions:

```powershell
$env:Storage__AccountUri = "https://<account>.blob.core.windows.net"
dotnet run --project src/SwissRainRadar.Web
```

The application expects private `raw` and `maps` containers to exist. Terraform creates them in the production deployment.

Do not place account keys, connection strings or Terraform variable files in Git.

## Troubleshooting

### The page loads without a usable map

Confirm that `npm ci` and `npm run vendor` completed successfully. Missing files below `wwwroot/vendor/leaflet` cause browser 404 responses and prevent Leaflet from initializing.

### `/api/maps/latest` returns 503

This is expected before the first successful publication. Check the application log for unavailable STAC assets, download failures, HDF parsing errors or an incomplete sequence of hourly grids.

### No files appear in `App_Data`

Check the log and the effective `Storage:AccountUri`. A non-empty account URI switches the application to Azure Blob Storage instead of local files. Also verify the process content root shown in the ASP.NET startup log.

### The worker makes no updates after a pause

The timer exists only in the running ASP.NET process. Locally, stopping `dotnet run` stops the worker. In Azure, `always_on = false` allows an idle application to sleep; the next request restarts it and triggers an immediate update.

### OpenStreetMap is blank but the application is healthy

Leaflet is served locally, but OpenStreetMap tiles are fetched directly by the browser. Check browser network access, content-security-policy errors and the tile service response.
