// Time-zone helpers. The API stores every moment in UTC; people read the SHOP's local time.
// We never use the browser's own zone for this, so the screen shows the same hours for everyone.

export const DEFAULT_TIME_ZONE = 'Asia/Kolkata';

/** "2026-11-03T09:00:00" (no Z) is treated as UTC, which is what the API means. */
export function parseUtc(value: string): Date {
  const hasZone = /Z$|[+-]\d{2}:\d{2}$/.test(value);
  return new Date(hasZone ? value : value + 'Z');
}

interface Parts { year: number; month: number; day: number; hour: number; minute: number; second: number; }

function partsIn(date: Date, timeZone: string): Parts {
  const formatter = new Intl.DateTimeFormat('en-CA', {
    timeZone, hourCycle: 'h23',
    year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit'
  });
  const out: Record<string, number> = {};
  for (const p of formatter.formatToParts(date)) {
    if (p.type !== 'literal') out[p.type] = Number(p.value);
  }
  return { year: out['year'], month: out['month'], day: out['day'], hour: out['hour'], minute: out['minute'], second: out['second'] };
}

/** How far ahead of UTC the zone is at that moment, in milliseconds (India: +5:30 = 19 800 000). */
export function offsetMs(date: Date, timeZone: string): number {
  const p = partsIn(date, timeZone);
  const asIfUtc = Date.UTC(p.year, p.month - 1, p.day, p.hour, p.minute, p.second);
  return asIfUtc - Math.floor(date.getTime() / 1000) * 1000;
}

/** The UTC moment when it is `minutesOfDay` on `date` (YYYY-MM-DD) on the clocks of `timeZone`. */
export function zonedToUtc(date: string, minutesOfDay: number, timeZone: string): Date {
  const [y, m, d] = date.split('-').map(Number);
  const naive = Date.UTC(y, m - 1, d, 0, 0, 0) + minutesOfDay * 60_000;
  let result = naive - offsetMs(new Date(naive), timeZone);
  // Around a daylight-saving change the first guess can be an hour off; one correction settles it.
  const second = naive - offsetMs(new Date(result), timeZone);
  if (second !== result) result = second;
  return new Date(result);
}

/** [start, end) of one local calendar day, as UTC moments. */
export function dayRangeUtc(date: string, timeZone: string): { from: Date; to: Date } {
  return { from: zonedToUtc(date, 0, timeZone), to: zonedToUtc(addDays(date, 1), 0, timeZone) };
}

/** Adds whole days to a YYYY-MM-DD string (calendar arithmetic, no zones involved). */
export function addDays(date: string, days: number): string {
  const [y, m, d] = date.split('-').map(Number);
  const moved = new Date(Date.UTC(y, m - 1, d + days));
  return moved.toISOString().slice(0, 10);
}

export function todayIn(timeZone: string, now: Date = new Date()): string {
  const p = partsIn(now, timeZone);
  return `${p.year}-${String(p.month).padStart(2, '0')}-${String(p.day).padStart(2, '0')}`;
}

/** Minutes since local midnight for a UTC moment. */
export function minutesOfDayIn(date: Date, timeZone: string): number {
  const p = partsIn(date, timeZone);
  return p.hour * 60 + p.minute;
}

/** The local calendar date (YYYY-MM-DD) of a UTC moment. */
export function localDateOf(date: Date, timeZone: string): string {
  const p = partsIn(date, timeZone);
  return `${p.year}-${String(p.month).padStart(2, '0')}-${String(p.day).padStart(2, '0')}`;
}

export function formatTime(value: string | Date, timeZone: string): string {
  const date = typeof value === 'string' ? parseUtc(value) : value;
  return new Intl.DateTimeFormat('en-IN', { timeZone, hour: 'numeric', minute: '2-digit', hour12: true }).format(date).replace(/\s?([ap])m/i, (_, x: string) => ' ' + x.toLowerCase() + 'm');
}

export function formatDate(value: string | Date, timeZone: string): string {
  const date = typeof value === 'string' ? parseUtc(value) : value;
  return new Intl.DateTimeFormat('en-IN', { timeZone, weekday: 'short', day: 'numeric', month: 'short', year: 'numeric' }).format(date);
}

export function formatDateTime(value: string | Date, timeZone: string): string {
  return `${formatDate(value, timeZone)}, ${formatTime(value, timeZone)}`;
}

/** "9 am", "12 pm", "6 pm" for the hour ruler. */
export function formatHour(hour: number): string {
  const h = ((hour % 24) + 24) % 24;
  const suffix = h < 12 ? 'am' : 'pm';
  const twelve = h % 12 === 0 ? 12 : h % 12;
  return `${twelve} ${suffix}`;
}

/** Value for <input type="datetime-local">, read as the shop's local time. */
export function localInputToUtc(value: string, timeZone: string): Date {
  const [date, time] = value.split('T');
  const [h, m] = (time ?? '00:00').split(':').map(Number);
  return zonedToUtc(date, h * 60 + m, timeZone);
}

export function utcToLocalInput(value: Date, timeZone: string): string {
  const p = partsIn(value, timeZone);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${p.year}-${pad(p.month)}-${pad(p.day)}T${pad(p.hour)}:${pad(p.minute)}`;
}
