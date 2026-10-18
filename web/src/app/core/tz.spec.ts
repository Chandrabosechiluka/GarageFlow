import { addDays, dayRangeUtc, localDateOf, minutesOfDayIn, parseUtc, todayIn, zonedToUtc } from './tz';

const IST = 'Asia/Kolkata';

describe('tz helpers', () => {
  it('treats a timestamp without a zone as UTC', () => {
    expect(parseUtc('2026-11-03T09:00:00').toISOString()).toBe('2026-11-03T09:00:00.000Z');
    expect(parseUtc('2026-11-03T09:00:00Z').toISOString()).toBe('2026-11-03T09:00:00.000Z');
  });

  it('converts shop-local wall time to UTC', () => {
    expect(zonedToUtc('2026-10-19', 9 * 60, IST).toISOString()).toBe('2026-10-19T03:30:00.000Z');
  });

  it('gives the UTC range of a local day', () => {
    const { from, to } = dayRangeUtc('2026-10-19', IST);
    expect(from.toISOString()).toBe('2026-10-18T18:30:00.000Z');
    expect(to.toISOString()).toBe('2026-10-19T18:30:00.000Z');
  });

  it('adds days across a month end', () => {
    expect(addDays('2026-10-31', 1)).toBe('2026-11-01');
    expect(addDays('2026-03-01', -1)).toBe('2026-02-28');
  });

  it("uses the shop's date, not the browser's", () => {
    const now = new Date('2026-10-18T20:00:00Z');
    expect(todayIn(IST, now)).toBe('2026-10-19');
    expect(todayIn('UTC', now)).toBe('2026-10-18');
  });

  it('reads local minutes and date from a UTC moment', () => {
    const moment = new Date('2026-10-19T03:30:00Z');
    expect(minutesOfDayIn(moment, IST)).toBe(9 * 60);
    expect(localDateOf(new Date('2026-10-18T19:00:00Z'), IST)).toBe('2026-10-19');
  });
});
