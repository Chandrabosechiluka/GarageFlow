// Pure maths for the bay board, kept apart from the screen so it can be unit-tested.

export interface TimeWindow { startMin: number; endMin: number; }
export interface Placed { leftPct: number; widthPct: number; }

const DEFAULT_START = 8 * 60;
const DEFAULT_END = 19 * 60;

/** The hours the board shows: 8 am to 7 pm, widened (to whole hours) when an appointment falls outside. */
export function windowFor(items: { startMin: number; endMin: number }[]): TimeWindow {
  let start = DEFAULT_START;
  let end = DEFAULT_END;
  for (const item of items) {
    start = Math.min(start, Math.floor(item.startMin / 60) * 60);
    end = Math.max(end, Math.ceil(item.endMin / 60) * 60);
  }
  return { startMin: Math.max(0, start), endMin: Math.min(24 * 60, end) };
}

/** Where a block sits inside the day, as percentages of the board width. */
export function place(startMin: number, endMin: number, window: TimeWindow): Placed {
  const span = window.endMin - window.startMin;
  const left = ((startMin - window.startMin) / span) * 100;
  const width = ((endMin - startMin) / span) * 100;
  return { leftPct: clamp(left), widthPct: Math.max(0.5, Math.min(width, 100 - clamp(left))) };
}

/** Hour marks for the ruler: { hour: 9, leftPct: 8.3 }, ... */
export function hourTicks(window: TimeWindow): { hour: number; leftPct: number }[] {
  const ticks: { hour: number; leftPct: number }[] = [];
  const span = window.endMin - window.startMin;
  for (let hour = Math.ceil(window.startMin / 60); hour * 60 <= window.endMin; hour++) {
    ticks.push({ hour, leftPct: ((hour * 60 - window.startMin) / span) * 100 });
  }
  return ticks;
}

/** Where the "now" line goes, or null when now is outside the shown hours. */
export function nowPosition(nowMin: number, window: TimeWindow): number | null {
  if (nowMin < window.startMin || nowMin > window.endMin) return null;
  return ((nowMin - window.startMin) / (window.endMin - window.startMin)) * 100;
}

function clamp(value: number): number {
  return Math.max(0, Math.min(100, value));
}
