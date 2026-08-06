/****************************************************************
 * 轻量 cron 调度解析器
 * 支持 5 段或 6 段表达式：
 * - 5 段：minute hour day-of-month month day-of-week
 * - 6 段：second minute hour day-of-month month day-of-week
 ****************************************************************/
function normalizeWeekday(value) {
  const n = Number(value);
  if (!Number.isFinite(n)) return null;
  if (n === 7) return 0;
  return n;
}

function pad2(value) {
  return String(value).padStart(2, "0");
}

function toInt(value) {
  const n = Number(value);
  return Number.isInteger(n) ? n : null;
}

function expandWildcard(min, max, step = 1) {
  const values = [];
  for (let value = min; value <= max; value += step) values.push(value);
  return values;
}

function parseNumberToken(token, fieldName) {
  if (fieldName === "dow") return normalizeWeekday(token);
  const n = toInt(token);
  return Number.isInteger(n) ? n : null;
}

function parsePart(rawText, min, max, fieldName) {
  const text = String(rawText || "").trim();
  const part = text === "?" || text === "" ? "*" : text;
  const tokens = part.split(",").map(token => token.trim()).filter(Boolean);
  if (!tokens.length) {
    return { raw: text, any: true, values: expandWildcard(min, max), type: "any" };
  }

  const values = new Set();
  const descriptors = [];
  let any = false;

  for (const token of tokens) {
    if (token === "*") {
      any = true;
      expandWildcard(min, max).forEach(value => values.add(value));
      descriptors.push({ kind: "any" });
      continue;
    }

    const [rangeText, stepText] = token.split("/").map(item => item.trim());
    const step = stepText ? toInt(stepText) : 1;
    if (!Number.isInteger(step) || step <= 0) continue;

    if (rangeText === "*") {
      any = true;
      expandWildcard(min, max, step).forEach(value => values.add(value));
      descriptors.push({ kind: "step", start: min, end: max, step });
      continue;
    }

    if (rangeText.includes("-")) {
      const [startText, endText] = rangeText.split("-").map(item => item.trim());
      const start = parseNumberToken(startText, fieldName);
      const end = parseNumberToken(endText, fieldName);
      if (!Number.isInteger(start) || !Number.isInteger(end)) continue;
      const limitedStart = Math.max(min, Math.min(max, start));
      const limitedEnd = Math.max(min, Math.min(max, end));
      const rangeValues = [];
      for (let value = limitedStart; value <= limitedEnd; value += step) {
        rangeValues.push(value);
        values.add(value);
      }
      descriptors.push({ kind: "range", start: limitedStart, end: limitedEnd, step, values: rangeValues });
      continue;
    }

    const fixed = parseNumberToken(rangeText, fieldName);
    if (!Number.isInteger(fixed)) continue;
    const normalized = fieldName === "dow" ? normalizeWeekday(fixed) : fixed;
    if (!Number.isInteger(normalized) || normalized < min || normalized > max) continue;
    values.add(normalized);
    descriptors.push({ kind: "value", value: normalized });
  }

  const sortedValues = Array.from(values).sort((a, b) => a - b);
  return {
    raw: text,
    any,
    values: sortedValues,
    descriptors,
    type: any ? "any" : (descriptors.length === 1 ? descriptors[0].kind : "mixed")
  };
}

function nextValue(values, current) {
  for (const value of values) {
    if (value >= current) return value;
  }
  return null;
}

function firstDescriptor(part) {
  return Array.isArray(part?.descriptors) && part.descriptors.length ? part.descriptors[0] : null;
}

export class CronSchedule {
  constructor(expression, parts) {
    this.expression = String(expression || "").trim();
    this.parts = parts;
  }

  static parse(expression) {
    const text = String(expression || "").trim();
    if (!text) return null;
    const fields = text.split(/\s+/).filter(Boolean);
    if (fields.length !== 5 && fields.length !== 6) return null;

    const hasSeconds = fields.length === 6;
    const parts = hasSeconds
      ? {
          second: parsePart(fields[0], 0, 59, "second"),
          minute: parsePart(fields[1], 0, 59, "minute"),
          hour: parsePart(fields[2], 0, 23, "hour"),
          dayOfMonth: parsePart(fields[3], 1, 31, "dom"),
          month: parsePart(fields[4], 1, 12, "month"),
          dayOfWeek: parsePart(fields[5], 0, 6, "dow")
        }
      : {
          second: parsePart("0", 0, 59, "second"),
          minute: parsePart(fields[0], 0, 59, "minute"),
          hour: parsePart(fields[1], 0, 23, "hour"),
          dayOfMonth: parsePart(fields[2], 1, 31, "dom"),
          month: parsePart(fields[3], 1, 12, "month"),
          dayOfWeek: parsePart(fields[4], 0, 6, "dow")
        };

    return new CronSchedule(text, parts);
  }

  static describe(expression) {
    const schedule = CronSchedule.parse(expression);
    return schedule ? schedule.describe() : "";
  }

  matches(date) {
    const value = date instanceof Date ? date : new Date(date);
    if (Number.isNaN(value.getTime())) return false;
    const second = value.getSeconds();
    const minute = value.getMinutes();
    const hour = value.getHours();
    const dayOfMonth = value.getDate();
    const month = value.getMonth() + 1;
    const dayOfWeek = value.getDay();
    return this.parts.second.values.includes(second)
      && this.parts.minute.values.includes(minute)
      && this.parts.hour.values.includes(hour)
      && this.parts.dayOfMonth.values.includes(dayOfMonth)
      && this.parts.month.values.includes(month)
      && this.parts.dayOfWeek.values.includes(dayOfWeek);
  }

  nextAfter(referenceDate) {
    const base = referenceDate instanceof Date ? new Date(referenceDate.getTime()) : new Date(referenceDate || Date.now());
    if (Number.isNaN(base.getTime())) return null;
    let candidate = new Date(base.getTime() + 1000);
    candidate.setMilliseconds(0);

    for (let guard = 0; guard < 200000; guard += 1) {
      if (candidate.getMonth() + 1 > 12) return null;
      if (!this.parts.month.values.includes(candidate.getMonth() + 1)) {
        const nextMonth = nextValue(this.parts.month.values, candidate.getMonth() + 1);
        if (nextMonth === null) {
          candidate = new Date(candidate.getFullYear() + 1, this.parts.month.values[0] - 1, 1, 0, 0, 0, 0);
          continue;
        }
        candidate = new Date(candidate.getFullYear(), nextMonth - 1, 1, 0, 0, 0, 0);
        continue;
      }

      const daysInMonth = new Date(candidate.getFullYear(), candidate.getMonth() + 1, 0).getDate();
      if (candidate.getDate() > daysInMonth) {
        candidate = new Date(candidate.getFullYear(), candidate.getMonth() + 1, 1, 0, 0, 0, 0);
        continue;
      }

      const dayMatches = this.parts.dayOfMonth.values.includes(candidate.getDate())
        && this.parts.dayOfWeek.values.includes(candidate.getDay());
      if (!dayMatches) {
        candidate = new Date(candidate.getFullYear(), candidate.getMonth(), candidate.getDate() + 1, 0, 0, 0, 0);
        continue;
      }

      if (!this.parts.hour.values.includes(candidate.getHours())) {
        const nextHour = nextValue(this.parts.hour.values, candidate.getHours());
        if (nextHour === null) {
          candidate = new Date(candidate.getFullYear(), candidate.getMonth(), candidate.getDate() + 1, this.parts.hour.values[0], 0, 0, 0);
          continue;
        }
        candidate = new Date(candidate.getFullYear(), candidate.getMonth(), candidate.getDate(), nextHour, 0, 0, 0);
        continue;
      }

      if (!this.parts.minute.values.includes(candidate.getMinutes())) {
        const nextMinute = nextValue(this.parts.minute.values, candidate.getMinutes());
        if (nextMinute === null) {
          const nextHour = nextValue(this.parts.hour.values, candidate.getHours() + 1);
          if (nextHour === null) {
            candidate = new Date(candidate.getFullYear(), candidate.getMonth(), candidate.getDate() + 1, this.parts.hour.values[0], this.parts.minute.values[0], 0, 0);
            continue;
          }
          candidate = new Date(candidate.getFullYear(), candidate.getMonth(), candidate.getDate(), nextHour, this.parts.minute.values[0], 0, 0);
          continue;
        }
        candidate = new Date(candidate.getFullYear(), candidate.getMonth(), candidate.getDate(), candidate.getHours(), nextMinute, 0, 0);
        continue;
      }

      if (!this.parts.second.values.includes(candidate.getSeconds())) {
        const nextSecond = nextValue(this.parts.second.values, candidate.getSeconds());
        if (nextSecond === null) {
          const nextMinute = nextValue(this.parts.minute.values, candidate.getMinutes() + 1);
          if (nextMinute === null) {
            const nextHour = nextValue(this.parts.hour.values, candidate.getHours() + 1);
            if (nextHour === null) {
              candidate = new Date(candidate.getFullYear(), candidate.getMonth(), candidate.getDate() + 1, this.parts.hour.values[0], this.parts.minute.values[0], this.parts.second.values[0], 0);
              continue;
            }
            candidate = new Date(candidate.getFullYear(), candidate.getMonth(), candidate.getDate(), nextHour, this.parts.minute.values[0], this.parts.second.values[0], 0);
            continue;
          }
          candidate = new Date(candidate.getFullYear(), candidate.getMonth(), candidate.getDate(), candidate.getHours(), nextMinute, this.parts.second.values[0], 0);
          continue;
        }
        candidate = new Date(candidate.getFullYear(), candidate.getMonth(), candidate.getDate(), candidate.getHours(), candidate.getMinutes(), nextSecond, 0);
        continue;
      }

      if (this.matches(candidate)) return candidate;
      candidate = new Date(candidate.getTime() + 1000);
    }

    return null;
  }

  isDue(lastRunAt, now = Date.now()) {
    if (!this.expression) return false;
    const baseline = lastRunAt ? new Date(lastRunAt) : null;
    if (!baseline || Number.isNaN(baseline.getTime())) return true;
    const next = this.nextAfter(baseline);
    if (!next) return false;
    return next.getTime() <= Number(now || Date.now());
  }

  describe() {
    const { second, minute, hour, dayOfMonth, month, dayOfWeek } = this.parts;
    const sameDayLoop = month.any && dayOfMonth.any && dayOfWeek.any;
    const fixedMinute = minute.values.length === 1;
    const fixedSecondZero = second.values.length === 1 && second.values[0] === 0;

    let intervalSec = 0;
    const secondDesc = firstDescriptor(second);
    const minuteDesc = firstDescriptor(minute);
    const hourDesc = firstDescriptor(hour);

    if (sameDayLoop && hour.any && minute.any && secondDesc?.kind === "step") {
      intervalSec = (Number(secondDesc.step) || 0);
    } else if (sameDayLoop && hour.any && fixedSecondZero && minuteDesc?.kind === "step") {
      intervalSec = (Number(minuteDesc.step) || 0) * 60;
    } else if (sameDayLoop && fixedMinute && fixedSecondZero && (hourDesc?.kind === "step" || hourDesc?.kind === "range")) {
      intervalSec = (Number(hourDesc.step) || 0) * 3600;
    } else if (sameDayLoop && hour.any && fixedMinute && fixedSecondZero) {
      intervalSec = 3600;
    }

    if (intervalSec <= 0) return "";
    if (intervalSec >= 3600 && intervalSec % 3600 === 0) return `${intervalSec / 3600}小时`;
    if (intervalSec >= 60) return `${Math.round(intervalSec / 60)}分钟`;
    return "1分钟";
  }
}
