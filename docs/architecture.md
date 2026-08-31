# Architecture decisions

## ADR-001: ASP.NET hosted updater

Use an ASP.NET `BackgroundService` in the same App Service process as the public website. This minimizes Azure resources and intentionally permits updates to pause when the site is idle. A separate Function, WebJob or scheduler is not required in version 1.

## ADR-002: Private Blob Storage

Store source HDF5 files and generated maps in private Blob containers. Serve map images through ASP.NET instead of exposing the storage account. Authenticate the app with Managed Identity and disable shared-key authorization.

## ADR-003: Non-overlapping CPC windows

CPC files are published every five minutes but each contains a 60-minute accumulation. Select one file per hour, aligned to the newest product minute, when computing 3–24-hour totals; summing every publication would over-count rainfall approximately twelve times.

## ADR-004: Managed HDF5 reader

Use PureHDF to avoid native HDF5 binaries in Linux App Service. Keep HDF access behind `HdfRadarReader` so the implementation can be replaced if MeteoSwiss introduces a feature unsupported by the library.

## ADR-005: WGS84 output image

The source grid uses LV95. The renderer converts every regular WGS84 output pixel to LV95 using the official swisstopo approximation and samples the one-kilometre source grid. This produces an image that aligns with Leaflet's geographic `ImageOverlay` without requiring GDAL or PROJ native packages.

## ADR-006: OIDC deployment

Use a user-assigned Azure identity with GitHub federated credentials for `main` and pull requests. Do not store a service-principal secret or App Service publish profile in GitHub.

## Scale-out boundary

Version 1 fixes App Service to one worker. Multiple workers would each start the hosted updater and could race while writing blobs. A later scale-out version must acquire an Azure Blob lease or move importing into a singleton service before increasing `worker_count`.
