import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { errorMessage } from '../../core/api-error';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { LookupService } from '../../core/lookup.service';
import { ALL_WAITLIST_STATUSES, WaitlistEntry, WaitlistStatus } from '../../core/models';
import { WAITLIST_STATUS } from '../../core/status';
import { ToastService } from '../../core/toast.service';
import { formatDateTime } from '../../core/tz';
import { BadgeComponent } from '../../shared/badge.component';
import { EmptyComponent } from '../../shared/empty.component';
import { PageHeaderComponent } from '../../shared/page-header.component';

@Component({
  selector: 'gf-waitlist',
  standalone: true,
  imports: [RouterLink, BadgeComponent, EmptyComponent, PageHeaderComponent],
  template: `
    <gf-page-header title="Waitlist" subtitle="Customers waiting for a time to open up. When one does, they are offered it for two hours.">
      <button type="button" class="btn ghost" (click)="load()" [disabled]="loading()">Refresh</button>
      @if (auth.canBook()) { <a class="btn" routerLink="/book">New booking</a> }
    </gf-page-header>

    <section class="section plain">
      <div class="filters">
        <div class="field">
          <label for="ws">Status</label>
          <select id="ws" (change)="setStatus($any($event.target).value)">
            <option value="" [selected]="status() === ''">Any status</option>
            @for (s of statuses; track s) { <option [value]="s" [selected]="status() === s">{{ meta(s).label }}</option> }
          </select>
        </div>
      </div>

      @if (error()) { <div class="banner error" role="alert">{{ error() }}</div> }
      @if (loading()) { <p class="loading">Loading the waitlist…</p> }

      @if (!loading() && !error()) {
        @if (entries().length === 0) {
          <gf-empty title="Nobody is waiting" text="When a day is fully booked, put the customer on the waitlist from the booking screen." />
        } @else {
          <div class="table-wrap">
            <table class="data">
              <thead><tr><th>Customer and car</th><th>Service</th><th>Wants a start between</th><th>Status</th><th>Offer</th><th></th></tr></thead>
              <tbody>
                @for (e of entries(); track e.id) {
                  <tr>
                    <td><a class="row-link" [routerLink]="['/customers', e.customerId]">{{ lookups.customerName(e.customerId) }}</a><br><span class="muted small">{{ lookups.vehicleLabel(e.customerId, e.vehicleId) }}</span></td>
                    <td>{{ lookups.serviceName(e.serviceTypeId) }}</td>
                    <td class="small">{{ dt(e.earliestStartUtc) }}<br>to {{ dt(e.latestStartUtc) }}</td>
                    <td><gf-badge [tone]="meta(e.status).tone" [label]="meta(e.status).label" /></td>
                    <td class="small">
                      @if (e.offeredStartUtc) { {{ dt(e.offeredStartUtc) }}<br><span class="muted">answer by {{ dt(e.offerExpiresUtc || '') }}</span> }
                      @else if (e.appointmentId) { <a [routerLink]="['/appointments', e.appointmentId]">View appointment</a> }
                    </td>
                    <td class="right nowrap">
                      @if (auth.canBook()) {
                        @if (e.status === 'Offered') {
                          <button type="button" class="btn sm" [disabled]="busyId() === e.id" (click)="accept(e)">Accept offer</button>
                        }
                        @if (e.status === 'Waiting' || e.status === 'Offered') {
                          <button type="button" class="btn danger-ghost sm" [disabled]="busyId() === e.id" (click)="cancel(e)">Remove</button>
                        }
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      }
    </section>
  `
})
export class WaitlistComponent implements OnInit {
  readonly lookups = inject(LookupService);
  readonly auth = inject(AuthService);
  private readonly api = inject(ApiService);
  private readonly toasts = inject(ToastService);

  readonly statuses = ALL_WAITLIST_STATUSES;
  readonly status = signal<WaitlistStatus | ''>('');
  readonly entries = signal<WaitlistEntry[]>([]);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly busyId = signal('');

  ngOnInit(): void { this.load(); }

  meta(s: WaitlistStatus) { return WAITLIST_STATUS[s]; }
  dt(value: string): string { return value ? formatDateTime(value, this.lookups.timeZone()) : ''; }

  setStatus(value: string): void {
    this.status.set(value as WaitlistStatus | '');
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.api.waitlist(this.status()).subscribe({
      next: (list) => {
        this.lookups.ensureCustomers(list.map((e) => e.customerId)).subscribe({
          next: () => this.show(list),
          error: () => this.show(list)
        });
      },
      error: (error: unknown) => {
        this.loading.set(false);
        this.error.set(errorMessage(error));
      }
    });
  }

  accept(entry: WaitlistEntry): void {
    this.busyId.set(entry.id);
    this.api.acceptOffer(entry.id).subscribe({
      next: () => {
        this.busyId.set('');
        this.toasts.success('Offer accepted: the appointment is booked.');
        this.load();
      },
      error: (error: unknown) => {
        this.busyId.set('');
        this.toasts.error(errorMessage(error));
        this.load();
      }
    });
  }

  cancel(entry: WaitlistEntry): void {
    this.busyId.set(entry.id);
    this.api.cancelWaitlist(entry.id).subscribe({
      next: () => {
        this.busyId.set('');
        this.toasts.success('Removed from the waitlist.');
        this.load();
      },
      error: (error: unknown) => {
        this.busyId.set('');
        this.toasts.error(errorMessage(error));
      }
    });
  }

  private show(list: WaitlistEntry[]): void {
    this.entries.set([...list].sort((a, b) => b.createdUtc.localeCompare(a.createdUtc)));
    this.loading.set(false);
  }
}

