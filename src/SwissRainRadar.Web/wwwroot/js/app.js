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
const timelineSelectedElement = document.querySelector("#timeline-selected");
const timelinePreviousElement = document.querySelector("#timeline-previous");
const timelineNextElement = document.querySelector("#timeline-next");

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

function updateTimelineControls() {
  const selectedIndex = timelineSnapshots.findIndex(
    snapshot => snapshot.periodEnd === selectedSnapshot?.periodEnd);
  const hasSelection = selectedIndex >= 0;

  timelinePreviousElement.disabled = !hasSelection || selectedIndex === 0;
  timelineNextElement.disabled = !hasSelection || selectedIndex === timelineSnapshots.length - 1;
  timelineSelectedElement.textContent = hasSelection
    ? formatTimestamp(selectedSnapshot.periodEnd)
    : "–";
  timelineSelectedElement.dateTime = hasSelection ? selectedSnapshot.periodEnd : "";
}

function selectSnapshot(snapshot) {
  const changed = selectedSnapshot?.periodEnd !== snapshot.periodEnd;
  selectedSnapshot = snapshot;
  updateTimelineControls();
  renderPeriods();
  periodEndElement.textContent = formatTimestamp(snapshot.periodEnd);

  const isLatest = snapshot.periodEnd === manifest.periodEnd;
  setStatus(isLatest ? "Aktuelle Daten" : "Historische Daten", "ready");

  const available = snapshot.maps.some(item => item.hours === selectedHours)
    ? selectedHours
    : snapshot.maps.at(-1)?.hours;
  if (available && (changed || !radarLayer)) showMap(available);
}

function moveTimeline(offset) {
  const selectedIndex = timelineSnapshots.findIndex(
    snapshot => snapshot.periodEnd === selectedSnapshot?.periodEnd);
  const nextSnapshot = timelineSnapshots[selectedIndex + offset];
  if (!nextSnapshot) return;

  followLatest = nextSnapshot === timelineSnapshots.at(-1);
  selectSnapshot(nextSnapshot);
}

timelinePreviousElement.addEventListener("click", () => moveTimeline(-1));
timelineNextElement.addEventListener("click", () => moveTimeline(1));

async function loadLatest() {
  try {
    const timelineRequest = fetch("/api/maps/timeline", { cache: "no-store" })
      .then(response => response.ok ? response.json() : { snapshots: [] })
      .catch(() => ({ snapshots: [] }));
    const response = await fetch("/api/maps/latest", { cache: "no-store" });
    if (!response.ok) throw new Error(`HTTP ${response.status}`);

    const nextManifest = await response.json();
    const timeline = await timelineRequest;
    const selectedPeriodEnd = selectedSnapshot && !followLatest
      ? selectedSnapshot.periodEnd
      : undefined;
    manifest = {
      ...nextManifest,
      maps: uniqueMapVariants(nextManifest.maps)
    };
    const latestSnapshot = { periodEnd: manifest.periodEnd, maps: manifest.maps };
    timelineSnapshots = normalizeSnapshots(timeline.snapshots ?? [], latestSnapshot);
    const preservedSnapshot = timelineSnapshots.find(snapshot => snapshot.periodEnd === selectedPeriodEnd);
    const snapshot = followLatest || !preservedSnapshot
      ? timelineSnapshots.at(-1)
      : preservedSnapshot;
    selectSnapshot(snapshot);
  } catch (error) {
    console.error(error);
    setStatus("Noch keine Karte verfügbar", "error");
    loadingElement.textContent = "Die erste Karte wird im Hintergrund erzeugt.";
  }
}

await loadLatest();
window.setInterval(loadLatest, 5 * 60 * 1000);
