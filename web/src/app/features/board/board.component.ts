import { Component, OnDestroy, computed, effect, inject, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { errorMessage } from '../../core/api-error';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { LookupService } from '../../core/lookup.service';
import { Appointment } from '../../core/models';
import { APPOINTMENT_STATUS } from '../../core/status';
import { ToastService } from '../../core/toast.service';
import { addDays, dayRangeUtc, formatDate, formatHour, formatTime, minutesOfDayIn, parseUtc, todayIn } from '../../core/tz';
import { BadgeComponent } from '../../shared/badge.component';
import { EmptyComponent } from '../../shared/empty.component';
import { PageHeaderComponent } from '../../shared/page-header.component';
import { hourTicks, nowPosition, place, windowFor } from './board-layout';

interface Block {
  appointment: Appointment;
  leftPct: number;
  widthPct: number;
  startMin: number;
  endMin: number;
}

@Component({
  selector: 'gf-board',
  standalone: true,
  imports: [RouterLink, BadgeComponent, EmptyComponent, PageHeaderComponent],
  template: `
    <gf-page-header [title]="titleText()" [subtitle]="subtitleText()">
      <button type="button" class="btn ghost sm" (click)="shift(-1)" aria-label="Previous day">‹ Previous</button>
      <button type="button" class="btn ghost sm" (click)="today()" [disabled]="isToday()">Today</button>
      <button type="button" class="btn ghost sm" (click)="shift(1)" aria-label="Next day">Next ›</button>
      <input class="date-input" type="date" [value]="date()" (change)="pick($any($event.target).value)" aria-label="Choose a day">
    </gf-page-header>

    @if (error()) {
      <div class="banner error" role="alert">{{ error() }}</div>
    }

    <p class="tally">
      <span><strong>{{ counts().booked }}</strong> expected</span>
      <span><strong>{{ counts().inShop }}</strong> in the shop</span>
      <span><strong>{{ counts().done }}</strong> finished</span>
      <label class="check small"><input type="checkbox" [checked]="showInactive()" (change)="showInactive.set(!showInactive())"> Show cancelled and no-shows</label>
    </p>

    <section class="board-wrap" aria-label="Bay board">
      <div class="board">
        <div class="ruler">
          <span class="bay-label"></span>
          <div class="track ruler-track">
            @for (tick of ticks(); track tick.hour) {
              <span class="tick" [style.left.%]="tick.leftPct"><em>{{ hourLabel(tick.hour) }}</em></span>
            }
          </div>
        </div>

        @for (row of rows(); track row.bayId) {
          <div class="lane">
            <span class="bay-label">{{ row.name }}</span>
            <div class="track">
              @for (tick of ticks(); track tick.hour) {
                <span class="gridline" [style.left.%]="tick.leftPct"></span>
              }
              @for (block of row.blocks; track block.appointment.id) {
                <a class="block" [attr.data-status]="block.appointment.status"
                   [routerLink]="['/appointments', block.appointment.id]"
                   [style.left.%]="block.leftPct" [style.width.%]="block.widthPct"
                   [attr.title]="blockTitle(block)" [attr.aria-label]="blockTitle(block)">
                  <span class="block-time">{{ timeRange(block.appointment) }}</span>
                  <span class="block-service">{{ lookups.serviceName(block.appointment.serviceTypeId) }}</span>
                  <span class="block-who">{{ lookups.customerName(block.appointment.customerId) }} · {{ lookups.technicianName(block.appointment.technicianId) }}</span>
                </a>
              }
            </div>
          </div>
        }

        @if (nowLeft() !== null) {
          <div class="now-layer" aria-hidden="true"><span class="now-line" [style.left]="'calc(var(--label-w) + (100% - var(--label-w)) * ' + nowLeft()! / 100 + ')'"></span></div>
        }
      </div>
      @if (loading()) { <p class="loading">Loading the day…</p> }
      @if (!loading() && rows().length === 0) {
        <gf-empty title="No bays yet" text="An admin needs to add bays in Shop setup before cars can be booked in." />
      }
    </section>

    <section class="section plain">
      <div class="section-head">
        <h2>The day's list</h2>
        @if (auth.canBook()) { <a class="btn sm" routerLink="/book">New booking</a> }
      </div>
      @if (queue().length === 0 && !loading()) {
        <gf-empty title="Nothing booked for this day" text="Pick another day, or book a car in.">
          @if (auth.canBook()) { <a class="btn" routerLink="/book">Book a car in</a> }
        </gf-empty>
      } @else {
        <div class="table-wrap">
          <table class="data">
            <thead><tr><th>Time</th><th>Customer and car</th><th>Service</th><th>Technician</th><th>Bay</th><th>Status</th><th></th></tr></thead>
            <tbody>
              @for (a of queue(); track a.id) {
                <tr>
                  <td class="nowrap">{{ timeRange(a) }}</td>
                  <td><a class="row-link" [routerLink]="['/appointments', a.id]">{{ lookups.customerName(a.customerId) }}</a><br><span class="muted small">{{ lookups.vehicleLabel(a.customerId, a.vehicleId) }}</span></td>
                  <td>{{ lookups.serviceName(a.serviceTypeId) }}</td>
                  <td>{{ lookups.technicianName(a.technicianId) }}</td>
                  <td>{{ lookups.bayName(a.bayId) }}</td>
                  <td><gf-badge [tone]="statusOf(a).tone" [label]="statusOf(a).label" /></td>
                  <td class="right">
                    @if (nextActionLabel(a); as label) {
                      <button type="button" class="btn sm" [disabled]="busyId() === a.id" (click)="runNext(a)">{{ label }}</button>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    </section>
  `,
  styles: [`
    :host { --label-w: 5.5rem; display: block; }
    .date-input { width: auto; }
    .tally { display: flex; gap: 1.5rem; flex-wrap: wrap; align-items: center; margin-bottom: 0.9rem; color: var(--ink-soft); }
    .tally strong { font-family: var(--font-display); font-size: 1.35rem; color: var(--ink); margin-right: 0.2rem; }
    .tally .check { margin-left: auto; }

    .board-wrap { background: var(--panel); border-top: 3px solid var(--ink); margin-bottom: 1.75rem; overflow-x: auto; }
    .board { position: relative; min-width: 46rem; padding: 0 1rem 0.5rem 0; }
    .ruler, .lane { display: flex; align-items: stretch; }
    .bay-label { flex: 0 0 var(--label-w); padding: 0 0.75rem; display: flex; align-items: center; font-family: var(--font-display); font-weight: 600; }
    .track { position: relative; flex: 1; }
    .ruler { height: 2rem; border-bottom: 1px solid var(--line-strong); }
    .ruler-track .tick { position: absolute; top: 0; bottom: 0; border-left: 1px solid var(--line-strong); }
    .ruler-track .tick em { font-style: normal; font-size: 0.8rem; color: var(--ink-soft); padding-left: 0.3rem; line-height: 2rem; white-space: nowrap; }
    .lane { height: 4.4rem; border-bottom: 1px solid var(--line); }
    .gridline { position: absolute; top: 0; bottom: 0; border-left: 1px solid var(--line); }

    .block {
      position: absolute; top: 0.4rem; bottom: 0.4rem; min-width: 1.5rem; padding: 0.2rem 0.5rem;
      display: flex; flex-direction: column; justify-content: center; gap: 1px; overflow: hidden;
      text-decoration: none; color: var(--ink); border-radius: 5px; border-left: 5px solid var(--steel); background: var(--steel-wash);
      line-height: 1.2; z-index: 2;
    }
    .block:hover { filter: brightness(0.96); box-shadow: 0 2px 8px rgba(13, 27, 36, 0.2); z-index: 3; }
    .block-time { font-size: 0.74rem; color: var(--ink-soft); white-space: nowrap; }
    .block-service { font-weight: 600; font-size: 0.86rem; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .block-who { font-size: 0.76rem; color: var(--ink-soft); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .block[data-status='CheckedIn'] { background: var(--petrol-wash); border-left-color: var(--petrol); }
    .block[data-status='InInspection'] { background: var(--signal-wash); border-left-color: var(--signal); }
    .block[data-status='Completed'] { background: var(--ok-wash); border-left-color: var(--ok); }
    .block[data-status='Cancelled'], .block[data-status='NoShow'] {
      background: repeating-linear-gradient(135deg, #e6eaec 0 6px, #d6dcdf 6px 12px); border-left-color: #8a979f; opacity: 0.8; z-index: 1;
    }
    .block[data-status='NoShow'] { border-left-color: var(--danger); }

    .now-layer { position: absolute; top: 0; bottom: 0; left: 0; right: 1rem; pointer-events: none; z-index: 4; }
    .now-line { position: absolute; top: 0; bottom: 0; width: 2px; background: var(--signal); box-shadow: 0 0 0 1px rgba(245, 184, 0, 0.25); }
    .now-line::before { content: ''; position: absolute; top: -1px; left: -4px; width: 10px; height: 10px; background: var(--signal); border-radius: 50%; }
  `]
})
export class BoardComponent implements OnDestroy {
  readonly lookups = inject(LookupService);
  readonly auth = inject(AuthService);
  private readonly api = inject(ApiService);
  private readonly toasts = inject(ToastService);

  readonly date = signal(todayIn(this.lookups.timeZone()));
  readonly appointments = signal<Appointment[]>([]);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly showInactive = signal(false);
  readonly busyId = signal('');
  private readonly nowMs = signal(Date.now());
  private picked = false;
  private readonly timer = setInterval(() => this.nowMs.set(Date.now()), 30_000);

  private readonly tz = this.lookups.timeZone;

  /** Appointments with their position on the board. */
  private readonly placed = computed(() => {
    const zone = this.tz();
    const visible = this.appointments().filter((a) => this.showInactive() || (a.status !== 'Cancelled' && a.status !== 'NoShow'));
    const measured = visible.map((a) => {
      const start = parseUtc(a.startTimeUtc);
      const end = parseUtc(a.endTimeUtc);
      const startMin = minutesOfDayIn(start, zone);
      const endMin = startMin + Math.max(1, Math.round((end.getTime() - start.getTime()) / 60000));
      return { appointment: a, startMin, endMin };
    });
    return measured;
  });

  readonly window = computed(() => windowFor(this.placed()));
  readonly ticks = computed(() => hourTicks(this.window()));

  readonly rows = computed(() => {
    const window = this.window();
    const placed = this.placed();
    return this.lookups.bays().map((bay) => ({
      bayId: bay.id,
      name: bay.name,
      blocks: placed
        .filter((p) => p.appointment.bayId === bay.id)
        .map<Block>((p) => ({ ...p, ...place(p.startMin, p.endMin, window) }))
    }));
  });

  readonly queue = computed(() =>
    [...this.appointments()]
      .filter((a) => this.showInactive() || (a.status !== 'Cancelled' && a.status !== 'NoShow'))
      .sort((a, b) => a.startTimeUtc.localeCompare(b.startTimeUtc))
  );

  readonly counts = computed(() => {
    const all = this.appointments();
    return {
      booked: all.filter((a) => a.status === 'Booked').length,
      inShop: all.filter((a) => a.status === 'CheckedIn' || a.status === 'InInspection').length,
      done: all.filter((a) => a.status === 'Completed').length
    };
  });

  readonly isToday = computed(() => this.date() === todayIn(this.tz()));

  readonly nowLeft = computed(() => {
    if (!this.isToday()) return null;
    return nowPosition(minutesOfDayIn(new Date(this.nowMs()), this.tz()), this.window());
  });

  readonly titleText = computed(() => (this.isToday() ? 'Today' : formatDate(this.fallbackInstant(), this.tz())));
  readonly subtitleText = computed(() => (this.isToday() ? formatDate(this.fallbackInstant(), this.tz()) : 'Bay board'));

  constructor() {
    // When the shop's time zone arrives and the person has not chosen a day, jump to the shop's "today".
    effect(() => {
      const zone = this.tz();
      untracked(() => {
        if (!this.picked) this.date.set(todayIn(zone));
      });
    });
    // Reload whenever the day (or the zone) changes.
    effect(() => {
      const day = this.date();
      const zone = this.tz();
      untracked(() => this.load(day, zone));
    });
  }

  ngOnDestroy(): void {
    clearInterval(this.timer);
  }

  statusOf(a: Appointment) {
    return APPOINTMENT_STATUS[a.status];
  }

  hourLabel(hour: number): string {
    return formatHour(hour);
  }

  timeRange(a: Appointment): string {
    return `${formatTime(a.startTimeUtc, this.tz())} – ${formatTime(a.endTimeUtc, this.tz())}`;
  }

  blockTitle(block: Block): string {
    const a = block.appointment;
    return `${this.timeRange(a)}: ${this.lookups.serviceName(a.serviceTypeId)} for ${this.lookups.customerName(a.customerId)}, ${this.lookups.technicianName(a.technicianId)} (${APPOINTMENT_STATUS[a.status].label})`;
  }

  shift(days: number): void {
    this.picked = true;
    this.date.set(addDays(this.date(), days));
  }

  today(): void {
    this.picked = true;
    this.date.set(todayIn(this.tz()));
  }

  pick(value: string): void {
    if (!value) return;
    this.picked = true;
    this.date.set(value);
  }

  nextActionLabel(a: Appointment): string | null {
    if (a.status === 'Booked' && this.auth.canBook()) return 'Check in';
    if (a.status === 'CheckedIn' && this.auth.canInspect()) return 'Start inspection';
    return null;
  }

  runNext(a: Appointment): void {
    const call: Observable<Appointment> | null =
      a.status === 'Booked' ? this.api.checkIn(a.id) : a.status === 'CheckedIn' ? this.api.startInspection(a.id) : null;
    if (!call) return;
    this.busyId.set(a.id);
    call.subscribe({
      next: (updated) => {
        this.appointments.update((list) => list.map((x) => (x.id === updated.id ? updated : x)));
        this.busyId.set('');
        this.toasts.success(updated.status === 'CheckedIn' ? 'Checked in.' : 'Inspection started.');
      },
      error: (error: unknown) => {
        this.busyId.set('');
        this.toasts.error(errorMessage(error));
      }
    });
  }

  /** Midday of the chosen day: a safe instant for formatting the date (never near midnight, so no zone surprises). */
  private fallbackInstant(): Date {
    return new Date(`${this.date()}T12:00:00Z`);
  }

  private load(day: string, zone: string): void {
    const { from, to } = dayRangeUtc(day, zone);
    this.loading.set(true);
    this.error.set('');
    this.api.appointments({ fromUtc: from.toISOString(), toUtc: to.toISOString(), page: 1, pageSize: 100 }).subscribe({
      next: (result) => {
        this.lookups.ensureCustomers(result.items.map((a) => a.customerId)).subscribe({
          next: () => this.finish(day, result.items),
          error: () => this.finish(day, result.items)
        });
      },
      error: (error: unknown) => {
        this.loading.set(false);
        this.error.set(errorMessage(error));
      }
    });
  }

  private finish(day: string, items: Appointment[]): void {
    if (day !== this.date()) return; // the person already moved to another day; ignore the late answer
    this.appointments.set(items);
    this.loading.set(false);
    // Names were filled into the cache after the items were read; nudge the views that depend on it.
    this.appointments.update((list) => [...list]);
  }
}
