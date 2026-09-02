# Services: Abhängigkeiten und Aktualisierungsablauf

Diese Dokumentation beschreibt die Service-Klassen in diesem Ordner und den Ablauf von
`RadarUpdateService.UpdateLatestAsync()`.

## Service-Abhängigkeiten

```mermaid
flowchart TD
    Worker[RadarUpdateWorker] -->|Scope erzeugen und Update starten| Update[RadarUpdateService]

    Update --> Time[TimeProvider]
    Time -.-> SystemTime[TimeProvider.System]
    Time -.-> FixedTime[FixedTimeProvider]

    Update --> Client[MeteoSwissClient]
    Client -->|STAC-JSON und HDF5-Dateien| MeteoSwiss[(MeteoSwiss)]

    Update --> Store[IObjectStore]
    Store -.-> FileStore[FileObjectStore]
    Store -.-> BlobStore[BlobObjectStore]

    Update --> Reader[HdfRadarReader]
    Update --> Aggregator[RainfallAggregator]
    Update --> Renderer[RadarImageRenderer]

    Renderer --> Projection[SwissProjection]
    Renderer --> Colors[RadarColorScale]
    Renderer --> Encoder[PngEncoder]
```

`RadarUpdateWorker` ist ein ASP.NET Core `BackgroundService`. Er löst die Aktualisierung beim
Start und anschließend im konfigurierten Intervall aus. Bei gesetztem
`Radar:FixedReferenceTimeUtc` kann der Worker stattdessen genau einen reproduzierbaren Lauf
ausführen.

Die konkrete Implementierung von `IObjectStore` wird in `Program.cs` gewählt:

- Ohne `Storage:AccountUri` wird `FileObjectStore` für das lokale Dateisystem registriert.
- Mit `Storage:AccountUri` wird `BlobObjectStore` für Azure Blob Storage registriert.

`RadarImageRenderer` verwendet drei statische Hilfsklassen: `SwissProjection` rechnet
WGS84-Koordinaten in LV95 um, `RadarColorScale` ordnet den Regenwerten RGBA-Farben zu und
`PngEncoder` erzeugt daraus die PNG-Datei.

## Ablauf von `UpdateLatestAsync()`

```mermaid
sequenceDiagram
    autonumber
    participant Worker as RadarUpdateWorker
    participant Update as RadarUpdateService
    participant Time as TimeProvider
    participant Client as MeteoSwissClient
    participant Meteo as MeteoSwiss
    participant Store as IObjectStore
    participant Processing as HDF / Aggregation / Renderer

    Worker->>Update: UpdateLatestAsync(token)
    Update->>Time: GetUtcNow()
    Time-->>Update: referenceTime

    loop Für jeden der letzten zwei Kalendertage
        Update->>Client: GetAssetsAsync(date, token)
        Client->>Meteo: STAC-Element abrufen
        Meteo-->>Client: JSON mit verfügbaren Assets
        Client-->>Update: sortierte RadarAsset-Liste
    end

    Update->>Update: Assets bis referenceTime filtern

    alt Keine Assets vorhanden
        Update->>Update: Warnung protokollieren
        Update-->>Worker: Rückkehr ohne neue Karte
    else Assets vorhanden
        Update->>Update: Zeiträume normalisieren und Stunden auswählen

        loop Für jedes ausgewählte Stunden-Asset
            Update->>Store: ExistsAsync("raw", path)
            Store-->>Update: vorhanden / nicht vorhanden

            alt Rohdatei fehlt
                Update->>Client: DownloadAsync(asset, token)
                Client->>Meteo: HDF5-Datei abrufen
                Meteo-->>Client: HDF5-Datenstrom
                Client-->>Update: Datenstrom
                Update->>Store: PutAsync("raw", path, HDF5)
            end

            Update->>Store: OpenReadAsync("raw", path)
            Store-->>Update: HDF5-Datenstrom
            Update->>Processing: HdfRadarReader.Read(stream)
            Processing-->>Update: RadarGrid
        end

        loop Für 1, 3, 6, 12 und 24 Stunden
            alt Genügend Stunden-Raster vorhanden
                Update->>Processing: RainfallAggregator.Sum(grids)
                Processing-->>Update: summiertes RadarGrid
                Update->>Processing: RadarImageRenderer.RenderAsync(grid)
                Processing-->>Update: PNG-Datenstrom
                Update->>Store: PutAsync("maps", history/.../Nh.png)
            else Zeitraum unvollständig
                Update->>Update: Warnung protokollieren und überspringen
            end
        end

        Update->>Store: PutAsync("maps", "latest.json")

        opt Mindestens eine Kartenvariante erzeugt
            Update->>Store: ReadTextAsync("maps", "timeline.json")
            Store-->>Update: vorhandene Timeline oder null

            alt Timeline fehlt
                Update->>Store: ListAsync("maps", "history/")
                Store-->>Update: Pfade vorhandener PNG-Karten
                Update->>Update: Timeline aus Pfaden aufbauen
            end

            Update->>Update: Snapshot einfügen und alte Einträge entfernen
            Update->>Store: PutAsync("maps", "timeline.json")
        end

        Update->>Update: Ergebnis protokollieren
        Update-->>Worker: Aktualisierung abgeschlossen
    end
```

Die Rohdaten werden unter `raw/yyyy/MM/dd/` gespeichert. Vorbereitete Karten liegen unter
`maps/history/yyyyMMddHHmm/`, während `maps/latest.json` auf den neuesten Stand und
`maps/timeline.json` auf die verfügbaren historischen Karten verweist.

## Zugehörige Dateien

- [`RadarUpdateWorker.cs`](RadarUpdateWorker.cs): Zeitsteuerung und Aufruf des Services
- [`RadarUpdateService.cs`](RadarUpdateService.cs): Orchestrierung der Aktualisierung
- [`MeteoSwissClient.cs`](MeteoSwissClient.cs): STAC-Abfrage und Download der HDF5-Dateien
- [`IObjectStore.cs`](IObjectStore.cs): Speicherabstraktion
- [`FileObjectStore.cs`](FileObjectStore.cs): Speicherung im lokalen Dateisystem
- [`BlobObjectStore.cs`](BlobObjectStore.cs): Speicherung in Azure Blob Storage
- [`HdfRadarReader.cs`](HdfRadarReader.cs): Einlesen des HDF5-Rasters
- [`RainfallAggregator.cs`](RainfallAggregator.cs): Bildung der Regensummen
- [`RadarImageRenderer.cs`](RadarImageRenderer.cs): Umwandlung eines Rasters in eine PNG-Karte
- [`RadarColorScale.cs`](RadarColorScale.cs): Farbzuordnung nach Niederschlagsmenge
- [`PngEncoder.cs`](PngEncoder.cs): Kodierung der RGBA-Pixel als PNG
- [`SwissProjection.cs`](SwissProjection.cs): Koordinatentransformation nach LV95
- [`FixedTimeProvider.cs`](FixedTimeProvider.cs): Konfigurierbare feste Referenzzeit
