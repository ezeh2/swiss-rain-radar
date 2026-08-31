import { cp, mkdir } from "node:fs/promises";

const source = new URL("../node_modules/leaflet/dist/", import.meta.url);
const target = new URL("../src/SwissRainRadar.Web/wwwroot/vendor/leaflet/", import.meta.url);

await mkdir(target, { recursive: true });
await cp(source, target, { recursive: true, force: true });

