import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { errorMessage } from '../../core/api-error';
import { ApiService } from '../../core/api.service';
import { LookupService } from '../../core/lookup.service';
import { AuditLog, PagedResult } from '../../core/models';
import { formatDateTime } from '../../core/tz';
import { EmptyComponent } from '../../shared/empty.component';
import { PageHeaderComponent } from '../../shared/page-header.component';
import { PagerComponent } from '../../shared/pager.component';

@Component({
  selector: 'gf-audit',
  standalone: true,
  imports: [EmptyComponent, PageHeaderComponent, PagerComponent],
  template: `
    <gf-page-header title="Audit trail" subtitle="Every change to the data, written automatically with who made it and when." />

    <section class="section plain">
      <div class="filters">
        <div class="field">
          <label for="entity">Record type</label>
          <select id="entity" (change)="setEntity($any($event.target).value)">
            <option value="" [selected]="entity() === ''">Everything</option>
            @for (e of entities; track e) { <option [value]="e" [selected]="entity() === e">{{ e }}</option> }
          </select>
        </div>
        <div class="field">
          <label for="by">Changed by (email)</label>
          <input id="by" type="search" placeholder="advisor@garageflow.local" [value]="by()" (input)="onBy($any($event.target).value)">
        </div>
        <div class="field">
          <label for="eid">Record id</label>
          <input id="eid" type="search" placeholder="Paste an id to follow one record" [value]="entityId()" (input)="onEntityId($any($event.target).value)">
        </div>
      </div>

      @if (error()) { <div class="banner error" role="alert">{{ error() }}</div> }
      @if (loading()) { <p class="loading">Loading the audit trail…</p> }

      @if (!loading() && result(); as r) {
        @if (r.items.length === 0) {
          <gf-empty title="Nothing recorded for this filter" text="Clear the filters to see all changes." />
        } @else {
          <div class="table-wrap">
            <table class="data">
              <thead><tr><th>When</th><th>Who</th><th>What</th><th>Record</th><th>Details</th></tr></thead>
              <tbody>
                @for (a of r.items; track a.id) {
                  <tr>
                    <td class="nowrap">{{ dt(a) }}</td>
                    <td>{{ a.performedBy }}</td>
                    <td>{{ a.action }}</td>
                    <td><strong>{{ a.entityName }}</strong><br><button type="button" class="link-btn small" (click)="followRecord(a.entityId)">{{ short(a.entityId) }}</button></td>
                    <td class="details">@if (a.details) { <details><summary>Show</summary><pre>{{ a.details }}</pre></details> }</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
        <gf-pager [page]="r.page" [pageSize]="r.pageSize" [totalPages]="r.totalPages" [totalCount]="r.totalCount" (pageChange)="goTo($event)" />
      }
    </section>
  `,
  styles: [`
    .details pre { white-space: pre-wrap; word-break: break-word; font-family: inherit; font-size: 0.82rem; background: var(--sunk); padding: 0.5rem 0.65rem; border-radius: 4px; margin: 0.4rem 0 0; max-width: 34rem; }
    .details summary { cursor: pointer; color: var(--petrol); }
  `]
})
export class AuditComponent implements OnInit, OnDestroy {
  private readonly api = inject(ApiService);
  private readonly lookups = inject(LookupService);

  readonly entities = ['Appointment', 'Customer', 'Vehicle', 'WaitlistEntry', 'AppUser', 'Technician', 'Bay', 'ServiceType'];
  readonly entity = signal('');
  readonly by = signal('');
  readonly entityId = signal('');
  readonly page = signal(1);
  readonly result = signal<PagedResult<AuditLog> | null>(null);
  readonly loading = signal(false);
  readonly error = signal('');
  private timer: ReturnType<typeof setTimeout> | undefined;

  ngOnInit(): void { this.load(); }
  ngOnDestroy(): void { clearTimeout(this.timer); }

  dt(a: AuditLog): string { return formatDateTime(a.timestampUtc, this.lookups.timeZone()); }
  short(id: string): string { return id.slice(0, 8); }

  setEntity(value: string): void { this.entity.set(value); this.page.set(1); this.load(); }
  onBy(value: string): void { this.by.set(value); this.debounced(); }
  onEntityId(value: string): void { this.entityId.set(value.trim()); this.debounced(); }
  followRecord(id: string): void { this.entityId.set(id); this.page.set(1); this.load(); }
  goTo(page: number): void { this.page.set(page); this.load(); }

  private debounced(): void {
    this.page.set(1);
    clearTimeout(this.timer);
    this.timer = setTimeout(() => this.load(), 300);
  }

  private load(): void {
    // An id that is not a complete GUID would be rejected by the API, so only send complete ones.
    const idOk = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(this.entityId());
    this.loading.set(true);
    this.error.set('');
    this.api.audit({
      entityName: this.entity() || undefined,
      performedBy: this.by().trim() || undefined,
      entityId: idOk ? this.entityId() : undefined,
      page: this.page(),
      pageSize: 20
    }).subscribe({
      next: (result) => {
        this.result.set(result);
        this.loading.set(false);
      },
      error: (error: unknown) => {
        this.loading.set(false);
        this.error.set(errorMessage(error));
      }
    });
  }
}
