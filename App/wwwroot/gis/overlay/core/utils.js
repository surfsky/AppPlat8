export function clamp(v, min, max) {
  return Math.max(min, Math.min(max, v));
}

export async function fetchWithTimeout(url, options = {}, timeoutMs = 12000) {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), timeoutMs);
  try {
    return await fetch(url, { ...options, signal: controller.signal });
  } finally {
    clearTimeout(timer);
  }
}

export function findNearestHourlyIndex(times) {
  const now = Date.now();
  let bestIdx = 0;
  let bestGap = Infinity;
  for (let i = 0; i < times.length; i++) {
    const gap = Math.abs(new Date(times[i]).getTime() - now);
    if (gap < bestGap) {
      bestGap = gap;
      bestIdx = i;
    }
  }
  return bestIdx;
}

export function addOrUpdateGeoJsonSource(map, sourceId, data) {
  const src = map.getSource(sourceId);
  if (!src) map.addSource(sourceId, { type: "geojson", data });
  else src.setData(data);
}

export function setInfo(id, text) {
  const el = document.getElementById(id);
  if (el) el.innerText = text;
}

export function chunkArray(arr, size) {
  const out = [];
  for (let i = 0; i < arr.length; i += size) {
    out.push(arr.slice(i, i + size));
  }
  return out;
}
