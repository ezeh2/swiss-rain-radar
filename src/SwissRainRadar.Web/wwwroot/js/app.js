const map = L.map("map", {
  zoomControl: true,
  minZoom: 5,
  maxZoom: 12,
  maxBounds: [[42.6, 1.4], [50.2, 13.7]],
  maxBoundsViscosity: 0.75
});

map.fitBounds([[45.55, 5.4], [47.95, 10.7]]);

L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
  maxZoom: 19,
  attribution: "&copy; OpenStreetMap-Mitwirkende"
}).addTo(map);

const statusElement = document.querySelector("#status");
const loadingElement = document.querySelector("#loading");
const periodEndElement = document.querySelector("#period-end");
const periodsElement = document.querySelector("#periods");
const timelineElement = document.querySelector("#timeline");
const timelineSelectedElement = document.querySelector("#timeline-selected");
const timelineStartElement = document.querySelector("#timeline-start");
const timelineEndElement = document.querySelector("#timeline-end");

let manifest;
let radarLayer;
let selectedHours = 24;
let timelineSnapshots = [];
let selectedSnapshot;
let followLatest = true;

const dateTimeFormatter = new Intl.DateTimeFormat("de-CH", {
  dateStyle: "medium",
  timeStyle: "short",
  timeZone: "Europe/Zurich"
});

function formatTimestamp(value) {
  return dateTimeFormatter.format(new Date(value));
}

function uniqueMapVariants(variants) {
  const variantsByHours = new Map();
  for (const variant of variants) {
    if (!variantsByHours.has(variant.hours)) {
      variantsByHours.set(variant.hours, variant);
    }
  }

  return [...variantsByHours.values()].sort((left, right) => left.hours - right.hours);
}

function setStatus(message, type = "") {
  statusElement.className = `status ${type}`.trim();
  statusElement.querySelector("span:last-child").textContent = message;
}

function showMap(hours) {
  const variant = selectedSnapshot?.maps.find(mapVariant => mapVariant.hours === hours);
  if (!variant) return;

  selectedHours = hours;
  document.querySelectorAll(".period-button").forEach(button => {
    const active = Number(button.dataset.hours) === hours;
    button.classList.toggle("active", active);
    button.setAttribute("aria-pressed", String(active));
  });

  if (radarLayer) map.removeLayer(radarLayer);
  loadingElement.textContent = "Karte wird geladen …";
  loadingElement.classList.remove("hidden");
  const bounds = [[manifest.bounds.south, manifest.bounds.west], [manifest.bounds.north, manifest.bounds.east]];
  radarLayer = L.imageOverlay(`${variant.imageUrl}?v=${encodeURIComponent(selectedSnapshot.periodEnd)}`, bounds, {
    opacity: 0.84,
    interactive: false,
    crossOrigin: false
  });
  radarLayer.once("load", () => loadingElement.classList.add("hidden"));
  radarLayer.addTo(map);
}

function renderPeriods() {
  periodsElement.replaceChildren();
  for (const variant of selectedSnapshot?.maps ?? []) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "period-button";
    button.dataset.hours = variant.hours;
    button.textContent = `${variant.hours} h`;
    const active = variant.hours === selectedHours;
    button.classList.toggle("active", active);
    button.setAttribute("aria-pressed", String(active));
    button.addEventListener("click", () => showMap(variant.hours));
    periodsElement.append(button);
  }
}

function normalizeSnapshots(snapshots, latestSnapshot) {
  const snapshotsByTime = new Map();
  for (const snapshot of snapshots) {
    if (snapshot?.periodEnd && Array.isArray(snapshot.maps) && snapshot.maps.length > 0) {
      snapshotsByTime.set(snapshot.periodEnd, {
        ...snapshot,
        maps: uniqueMapVariants(snapshot.maps)
      });
    }
  }

  snapshotsByTime.set(latestSnapshot.periodEnd, latestSnapshot);
  return [...snapshotsByTime.values()]
    .sort((left, right) => Date.parse(left.periodEnd) - Date.parse(right.periodEnd));
}

function findSnapshotAtOrBefore(epochSeconds) {
  let result = timelineSnapshots[0];
  let left = 0;
  let right = timelineSnapshots.length - 1;

  while (left <= right) {
    const middle = Math.floor((left + right) / 2);
    const candidate = timelineSnapshots[middle];
    if (Date.parse(candidate.periodEnd) / 1000 <= epochSeconds) {
      result = candidate;
      left = middle + 1;
    } else {
      right = middle - 1;
    }
  }

  return result;
}

function configureTimeline(requestedEpochSeconds) {
  const first = timelineSnapshots[0];
  const last = timelineSnapshots.at(-1);
  const minimum = Math.floor(Date.parse(first.periodEnd) / 1000);
  const maximum = Math.floor(Date.parse(last.periodEnd) / 1000);

  timelineElement.min = minimum;
  timelineElement.max = maximum;
  timelineElement.step = 5 * 60;
  timelineElement.disabled = minimum === maximum;
  timelineElement.value = Math.min(maximum, Math.max(minimum, requestedEpochSeconds ?? maximum));
  timelineStartElement.textContent = formatTimestamp(minimum * 1000);
  timelineEndElement.textContent = formatTimestamp(maximum * 1000);
  timelineSelectedElement.textContent = formatTimestamp(Number(timelineElement.value) * 1000);
}

function selectSnapshot(snapshot) {
  const changed = selectedSnapshot?.periodEnd !== snapshot.periodEnd;
  selectedSnapshot = snapshot;
  renderPeriods();
  periodEndElement.textContent = formatTimestamp(snapshot.periodEnd);

  const isLatest = snapshot.periodEnd === manifest.periodEnd;
  setStatus(isLatest ? "Aktuelle Daten" : "Historische Daten", "ready");

  const available = snapshot.maps.some(item => item.hours === selectedHours)
    ? selectedHours
    : snapshot.maps.at(-1)?.hours;
  if (available && (changed || !radarLayer)) showMap(available);
}

timelineElement.addEventListener("input", () => {
  timelineSelectedElement.textContent = formatTimestamp(Number(timelineElement.value) * 1000);
});

timelineElement.addEventListener("change", () => {
  const requestedEpochSeconds = Number(timelineElement.value);
  const snapshot = findSnapshotAtOrBefore(requestedEpochSeconds);
  followLatest = requestedEpochSeconds >= Number(timelineElement.max);
  selectSnapshot(snapshot);
});

async function loadLatest() {
  try {
    const timelineRequest = fetch("/api/maps/timeline", { cache: "no-store" })
      .then(response => response.ok ? response.json() : { snapshots: [] })
      .catch(() => ({ snapshots: [] }));
    const response = await fetch("/api/maps/latest", { cache: "no-store" });
    if (!response.ok) throw new Error(`HTTP ${response.status}`);

    const nextManifest = await response.json();
    const timeline = await timelineRequest;
    const requestedEpochSeconds = selectedSnapshot && !followLatest
      ? Number(timelineElement.value)
      : undefined;
    manifest = {
      ...nextManifest,
      maps: uniqueMapVariants(nextManifest.maps)
    };
    const latestSnapshot = { periodEnd: manifest.periodEnd, maps: manifest.maps };
    timelineSnapshots = normalizeSnapshots(timeline.snapshots ?? [], latestSnapshot);
    configureTimeline(requestedEpochSeconds);

    const snapshot = followLatest
      ? latestSnapshot
      : findSnapshotAtOrBefore(Number(timelineElement.value));
    selectSnapshot(snapshot);
  } catch (error) {
    console.error(error);
    setStatus("Noch keine Karte verfügbar", "error");
    loadingElement.textContent = "Die erste Karte wird im Hintergrund erzeugt.";
  }
}

await loadLatest();
window.setInterval(loadLatest, 5 * 60 * 1000);
