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

let manifest;
let radarLayer;
let selectedHours = 24;

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
  const variant = manifest.maps.find(mapVariant => mapVariant.hours === hours);
  if (!variant) return;

  selectedHours = hours;
  document.querySelectorAll(".period-button").forEach(button => {
    const active = Number(button.dataset.hours) === hours;
    button.classList.toggle("active", active);
    button.setAttribute("aria-pressed", String(active));
  });

  if (radarLayer) map.removeLayer(radarLayer);
  const bounds = [[manifest.bounds.south, manifest.bounds.west], [manifest.bounds.north, manifest.bounds.east]];
  radarLayer = L.imageOverlay(`${variant.imageUrl}?v=${encodeURIComponent(manifest.periodEnd)}`, bounds, {
    opacity: 0.84,
    interactive: false,
    crossOrigin: false
  });
  radarLayer.once("load", () => loadingElement.classList.add("hidden"));
  radarLayer.addTo(map);
}

function renderPeriods() {
  periodsElement.replaceChildren();
  for (const variant of manifest.maps) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "period-button";
    button.dataset.hours = variant.hours;
    button.textContent = `${variant.hours} h`;
    button.addEventListener("click", () => showMap(variant.hours));
    periodsElement.append(button);
  }
}

async function loadLatest() {
  try {
    const response = await fetch("/api/maps/latest", { cache: "no-store" });
    if (!response.ok) throw new Error(`HTTP ${response.status}`);

    const nextManifest = await response.json();
    const changed = manifest?.periodEnd !== nextManifest.periodEnd;
    manifest = {
      ...nextManifest,
      maps: uniqueMapVariants(nextManifest.maps)
    };
    renderPeriods();

    const end = new Date(manifest.periodEnd);
    periodEndElement.textContent = new Intl.DateTimeFormat("de-CH", {
      dateStyle: "medium",
      timeStyle: "short",
      timeZone: "Europe/Zurich"
    }).format(end);
    setStatus("Aktuelle Daten", "ready");

    if (changed || !radarLayer) {
      const available = manifest.maps.some(item => item.hours === selectedHours)
        ? selectedHours
        : manifest.maps.at(-1)?.hours;
      if (available) showMap(available);
    }
  } catch (error) {
    console.error(error);
    setStatus("Noch keine Karte verfügbar", "error");
    loadingElement.textContent = "Die erste Karte wird im Hintergrund erzeugt.";
  }
}

await loadLatest();
window.setInterval(loadLatest, 5 * 60 * 1000);
