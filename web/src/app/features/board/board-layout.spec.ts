import { hourTicks, nowPosition, place, windowFor } from './board-layout';

describe('board layout', () => {
  it('shows 8 to 19 by default', () => {
    expect(windowFor([])).toEqual({ startMin: 480, endMin: 1140 });
  });

  it('widens to whole hours for early and late appointments', () => {
    expect(windowFor([{ startMin: 7 * 60 + 30, endMin: 8 * 60 }, { startMin: 18 * 60, endMin: 20 * 60 + 15 }]))
      .toEqual({ startMin: 7 * 60, endMin: 21 * 60 });
  });

  it('places a block as a share of the width', () => {
    const w = { startMin: 480, endMin: 1080 }; // 10 hours
    const p = place(540, 600, w); // 9:00-10:00
    expect(p.leftPct).toBeCloseTo(10);
    expect(p.widthPct).toBeCloseTo(10);
  });

  it('makes one tick per hour including both ends', () => {
    const ticks = hourTicks({ startMin: 480, endMin: 600 });
    expect(ticks.map((t) => t.hour)).toEqual([8, 9, 10]);
    expect(ticks[0].leftPct).toBe(0);
    expect(ticks[2].leftPct).toBe(100);
  });

  it('hides the now line outside the window', () => {
    const w = { startMin: 480, endMin: 1080 };
    expect(nowPosition(300, w)).toBeNull();
    expect(nowPosition(780, w)).toBeCloseTo(50);
  });
});
