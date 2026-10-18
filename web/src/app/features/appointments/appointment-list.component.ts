import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { errorMessage } from '../../core/api-error';
import { ApiService } from '../../core/api.service';
import { LookupService } from '../../core/lookup.service';
import { ALL_APPOINTMENT_STATUSES, Appointment, AppointmentStatus, PagedResult } from '../../core/models';
import { APPOINTMENT_STATUS } from '../../core/status';
import { addDays, dayRangeUtc, formatDate, formatTime, todayIn } from '../../core/tz';
import { BadgeComponent } from '../../shared/badge.component';
import { EmptyComponent } from '../../shared/empty.component';
import { PageHeaderComponent } from '../../shared/page-header.component';
import { PagerComponent } from '../../shared/pager.component';

@Component({
  selector: 'gf-appointment-list',
  standalone: true,
  imports: [RouterLink, BadgeComponent, EmptyComponent, PageHeaderComponent, PagerComponent],
  template: `
    <gf-page-header title="Appointments" subtitle="Everything booked, filtered by day and status.">
      <a class="btn" routerLink="/book">New booking</a>
    </gf-page-header>

    <section class="section plain">
      <div class="filters">
        <div class="field">
          <label for="from">From day</label>
          <input id="from" type="date" [value]="from()" (change)="setFrom($any($event.target).value)">
        </div>
        <div class="field">
          <label for="to">Until day</label>
          <input id="to" type="date" [value]="to()" (change)="setTo($any($event.target).value)">
        </div>
        <div class="field">
          <label for="status">Status</label>
          <select id="status" (change)="setStatus($any($event.target).value)">
            <option value="" [selected]="status() === ''">Any status</option>
            @for (s of statuses; track s) { <option [value]="s" [selected]="status() === s">{{ label(s) }}</option> }
          </select>
        </div>
        <button type="button" class="btn ghost" (click)="clear()">Clear filters</button>
      </div>

      @if (error()) { <div class="banner error" role="alert">{{ error() }}</div> }
      @if (loading()) { <p class="loading">Loading appointments…</p> }

      @if (!loading() && result(); as r) {
        @if (r.items.length === 0) {
          <gf-empty title="No appointments match" text="Widen the dates or clear the status filter." />
        } @else {
          <div class="table-wrap">
            <table class="data">
              <thead><tr><th>When</th><th>Customer and car</th><th>Service</th><th>Technician</th><th>Bay</th><th>Status</th></tr></thead>
              <tbody>
                @for (a of r.items; track a.id) {
                  <tr>
                    <td class="nowrap"><a class="row-link" [routerLink]="['/appointments', a.id]">{{ day(a) }}</a><br><span class="muted small">{{ time(a) }}</span></td>
                    <td>{{ lookups.customerName(a.customerId) }}<br><span class="muted small">{{ lookups.vehicleLabel(a.customerId, a.vehicleId) }}</span></td>
                    <td>{{ lookups.serviceName(a.serviceTypeId) }}</td>
                    <td>{{ lookups.technicianName(a.technicianId) }}</td>
                    <td>{{ lookups.bayName(a.bayId) }}</td>
                    <td><gf-badge [tone]="tone(a)" [label]="statusLabel(a)" /></td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
        <gf-pager [page]="r.page" [pageSize]="r.pageSize" [totalPages]="r.totalPages" [totalCount]="r.totalCount" (pageChange)="goTo($event)" />
      }
    </section>
  `
})
export class AppointmentListComponent implements OnInit {
  readonly lookups = inject(LookupService);
  private readonly api = inject(ApiService);

  readonly statuses = ALL_APPOINTMENT_STATUSES;
  readonly from = signal(todayIn(this.lookups.timeZone()));
  readonly to = signal('');
  readonly status = signal<AppointmentStatus | ''>('');
  readonly page = signal(1);
  readonly result = signal<PagedResult<Appointment> | null>(null);
  readonly loading = signal(false);
  readonly error = signal('');
  private readonly pageSize = 15;

  ngOnInit(): void {
    this.load();
  }

  label(s: AppointmentStatus): string { return APPOINTMENT_STATUS[s].label; }
  statusLabel(a: Appointment): string { return APPOINTMENT_STATUS[a.status].label; }
  tone(a: Appointment) { return APPOINTMENT_STATUS[a.status].tone; }
  day(a: Appointment): string { return formatDate(a.startTimeUtc, this.lookups.timeZone()); }
  time(a: Appointment): string { return `${formatTime(a.startTimeUtc, this.lookups.timeZone())} – ${formatTime(a.endTimeUtc, this.lookups.timeZone())}`; }

  setFrom(value: string): void { this.from.set(value); this.page.set(1); this.load(); }
  setTo(value: string): void { this.to.set(value); this.page.set(1); this.load(); }
  setStatus(value: string): void { this.status.set(value as AppointmentStatus | ''); this.page.set(1); this.load(); }
  clear(): void { this.from.set(''); this.to.set(''); this.status.set(''); this.page.set(1); this.load(); }
  goTo(page: number): void { this.page.set(page); this.load(); }

  private load(): void {
    const zone = this.lookups.timeZone();
    const fromUtc = this.from() ? dayRangeUtc(this.from(), zone).from.toISOString() : undefined;
    // "Until day" includes that whole day, so the range ends at the start of the next one.
    const toUtc = this.to() ? dayRangeUtc(addDays(this.to(), 0), zone).to.toISOString() : undefined;
    this.loading.set(true);
    this.error.set('');
    this.api.appointments({ fromUtc, toUtc, status: this.status(), page: this.page(), pageSize: this.pageSize }).subscribe({
      next: (result) => {
        this.lookups.ensureCustomers(result.items.map((a) => a.customerId)).subscribe({
          next: () => this.show(result),
          error: () => this.show(result)
        });
      },
      error: (error: unknown) => {
        this.loading.set(false);
        this.error.set(errorMessage(error));
      }
    });
  }

  private show(result: PagedResult<Appointment>): void {
    this.result.set(result);
    this.loading.set(false);
  }
}
