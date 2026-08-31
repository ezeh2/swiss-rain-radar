# Analysis of meteoradar.ch/regenkarten

Analysis date: 30 August 2026.

## Observed implementation

The examined page is mainly a server-rendered PHP page. Its rain map is returned by an image endpoint using query parameters for duration, end time and an optional Swiss coordinate marker:

```text
zeigeregenkarte.php?dauer=1440&hhmm=2140&koord_x=600&koord_y=200
```

The returned artifact is a 728 × 618 GIF containing the relief map, precipitation colors, labels, date range, legend and data completeness. The page offers 10, 20, 30, 60, 120, 180, 360, 720, 1440, 2880, 4320 and 5760-minute periods.

The page states that maps are updated every ten minutes and use weather-radar data plus ten-minute precipitation measurements from approximately 150 MeteoSwiss ground stations. This description does not disclose the exact fusion and interpolation algorithm, so an identical scientific reproduction cannot be inferred from the public web page.

## Public replacement data

MeteoSwiss publishes precipitation radar products through the Federal Spatial Data Infrastructure STAC API:

```text
https://data.geo.admin.ch/api/stac/v1/collections/ch.meteoschweiz.ogd-radar-precip
```

CombiPrecip is the closest public input because it combines five Swiss C-band weather radars with rain gauges. Files use ODIM HDF5, EPSG:2056/LV95, a rolling 14-day publication window and less than 1 MB per file according to the official documentation.

A sample inspected during planning contained:

- dataset `/dataset1/data1/data`
- dimensions 710 × 640
- 1 km grid size
- quantity `ACRR` in millimetres
- a 60-minute accumulation window
- 270 rain gauges used for that product

## Extracted color classes

The original image used these approximate discrete classes. The new project treats them as a starting point and uses transparency so the base map remains visible.

| Millimetres | RGB/hex |
|---:|---|
| 0–0.2 | `#A9BDFF` |
| 0.2–0.5 | `#7394FF` |
| 0.5–1 | `#3161FF` |
| 1–2 | `#AFFF61` |
| 2–4 | `#00E600` |
| 4–7 | `#00B900` |
| 7–10 | `#008700` |
| 10–20 | `#FFFF00` |
| 20–40 | `#FFC809` |
| 40–70 | `#FFA600` |
| 70–100 | `#FF8300` |
| 100–150 | `#D86B00` |
| 150–200 | `#FF0000` |
| 200–250 | `#B50000` |
| 250–300 | `#8B0000` |
| 300–350 | `#FF4BFF` |
| 350–400 | `#FF00FF` |
| 400–450 | `#B60089` |
| 450+ | `#8800FF` |

## Sources

- https://meteoradar.ch/regenkarten/
- https://opendatadocs.meteoswiss.ch/d-radar-data/d1-precipitation-radar-products
- https://data.geo.admin.ch/api/stac/v1/collections/ch.meteoschweiz.ogd-radar-precip
- https://docs.geo.admin.ch/visualize-data/wms.html

