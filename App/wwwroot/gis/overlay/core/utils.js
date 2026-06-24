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
  let latestTs = Number.NEGATIVE_INFINITY;
  for (let i = 0; i < times.length; i++) {
    const ts = new Date(times[i]).getTime();
    if (!Number.isFinite(ts)) continue;
    if (ts <= now && ts >= latestTs) {
      latestTs = ts;
      bestIdx = i;
    }
  }
  if (latestTs > Number.NEGATIVE_INFINITY) return bestIdx;
  let firstValid = 0;
  let firstTs = Infinity;
  for (let i = 0; i < times.length; i++) {
    const ts = new Date(times[i]).getTime();
    if (!Number.isFinite(ts)) continue;
    if (ts < firstTs) {
      firstTs = ts;
      firstValid = i;
    }
  }
  return firstValid;
}

/**获取时间序列步长（秒） */
export function getTimeSeriesStepSeconds(times) {
  const list = Array.isArray(times) ? times : [];
  if (list.length < 2) return 0;
  let prevTs = Number.NaN;
  for (let i = 0; i < list.length; i++) {
    const ts = new Date(list[i]).getTime();
    if (!Number.isFinite(ts)) continue;
    if (Number.isFinite(prevTs)) {
      const sec = Math.round(Math.abs(ts - prevTs) / 1000);
      if (sec > 0) return sec;
    }
    prevTs = ts;
  }
  return 0;
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
