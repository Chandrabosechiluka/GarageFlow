import { Component, Input, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { errorMessage } from '../../core/api-error';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { LookupService } from '../../core/lookup.service';
import { ALL_CONDITIONS, Appointment, AuditLog, Slot } from '../../core/models';
import { APPOINTMENT_STATUS, CONDITION, LIFECYCLE } from '../../core/status';
import { ToastService } from '../../core/toast.service';
import { formatDate, formatDateTime, formatTime, localDateOf, parseUtc } from '../../core/tz';
import { BadgeComponent } from '../../shared/badge.component';
import { EmptyComponent } from '../../shared/empty.component';
import { ModalComponent } from '../../shared/modal.component';
import { PageHeaderComponent } from '../../shared/page-header.component';
import { SlotPickerComponent } from '../../shared/slot-picker.component';

@Component({
  selector: 'gf-appointment-detail',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, BadgeComponent, EmptyComponent, ModalComponent, PageHeaderComponent, SlotPickerComponent],
  template: `
    @if (loading()) { <p class="loading">Loading the appointment…</p> }
    @if (error()) { <div class="banner error" role="alert">{{ error() }} <a routerLink="/appointments">Back to appointments</a></div> }

    @if (appointment(); as a) {
      <gf-page-header [title]="lookups.serviceName(a.serviceTypeId)" [subtitle]="whenText()">
        <a class="btn ghost" routerLink="/appointments">All appointments</a>
        <gf-badge [tone]="statusMeta().tone" [label]="statusMeta().label" />
      </gf-page-header>

      @if (a.status === 'Cancelled') {
        <div class="banner warn">Cancelled. Reason: {{ a.cancellationReason || 'none given' }}</div>
      } @else if (a.status === 'NoShow') {
        <div class="banner error">The customer did not arrive, so this was marked as a no-show.</div>
      } @else {
        <ol class="steps" aria-label="Progress">
          @for (step of steps(); track step.status) {
            <li [attr.data-state]="step.state"><span class="step-no">{{ step.number }}</span> {{ step.label }}</li>
          }
        </ol>
      }

      <div class="grid-2">
        <section class="section">
          <div class="section-head"><h2>Booking</h2></div>
          <dl class="facts">
            <dt>Customer</dt>
            <dd><a [routerLink]="['/customers', a.customerId]">{{ lookups.customerName(a.customerId) }}</a></dd>
            <dt>Car</dt><dd>{{ lookups.vehicleLabel(a.customerId, a.vehicleId) }}</dd>
            <dt>Service</dt><dd>{{ lookups.serviceName(a.serviceTypeId) }}</dd>
            <dt>Technician</dt><dd>{{ lookups.technicianName(a.technicianId) }}</dd>
            <dt>Bay</dt><dd>{{ lookups.bayName(a.bayId) }}</dd>
            <dt>Starts</dt><dd>{{ dateTime(a.startTimeUtc) }}</dd>
            <dt>Ends</dt><dd>{{ time(a.endTimeUtc) }}</dd>
          </dl>
        </section>

        <section class="section">
          <div class="section-head"><h2>Next steps</h2></div>
          <div class="btn-row">
            @if (a.status === 'Booked' && auth.canBook()) {
              <button type="button" class="btn" [disabled]="busy()" (click)="act('Checked in.', api.checkIn(a.id))">Check in</button>
              <button type="button" class="btn ghost" (click)="openReschedule()">Reschedule</button>
              <button type="button" class="btn ghost" [disabled]="busy()" (click)="markNoShow()">Mark as no-show</button>
              <button type="button" class="btn danger-ghost" (click)="modal.set('cancel')">Cancel appointment</button>
            }
            @if (a.status === 'CheckedIn') {
              @if (auth.canInspect()) {
                <button type="button" class="btn" [disabled]="busy()" (click)="act('Inspection started.', api.startInspection(a.id))">Start inspection</button>
              }
              @if (auth.canBook()) {
                <button type="button" class="btn danger-ghost" (click)="modal.set('cancel')">Cancel appointment</button>
              }
            }
            @if (a.status === 'InInspection' && auth.canInspect()) {
              <button type="button" class="btn" [disabled]="busy()" (click)="act('Appointment completed.', api.complete(a.id))">Complete appointment</button>
            }
          </div>
          @if (noActions()) {
            <p class="muted">There is nothing more to do here{{ a.status === 'Completed' ? ': the car has been serviced.' : '.' }}</p>
          }
        </section>
      </div>

      @if (a.inspectionItems.length > 0 || (a.status === 'InInspection' && auth.canInspect())) {
        <section class="section">
          <div class="section-head"><h2>Inspection</h2></div>

          @if (a.inspectionItems.length === 0) {
            <p class="muted">No findings recorded yet.</p>
          } @else {
            <div class="table-wrap">
              <table class="data">
                <thead><tr><th>Item</th><th>Condition</th><th>Notes</th></tr></thead>
                <tbody>
                  @for (item of a.inspectionItems; track item.id) {
                    <tr>
                      <td>{{ item.name }}</td>
                      <td><gf-badge [tone]="conditionMeta(item.condition).tone" [label]="conditionMeta(item.condition).label" /></td>
                      <td>{{ item.notes || '' }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }

          @if (a.status === 'InInspection' && auth.canInspect()) {
            <form class="inspect-form" [formGroup]="itemForm" (ngSubmit)="addItem()" novalidate>
              <h3>Record a finding</h3>
              <div class="form-row">
                <div class="field">
                  <label for="item-name">What was checked</label>
                  <input id="item-name" type="text" formControlName="name" placeholder="Front brake pads" [class.invalid]="itemForm.controls.name.invalid && itemForm.controls.name.touched">
                </div>
                <div class="field">
                  <label for="item-condition">Condition</label>
                  <select id="item-condition" formControlName="condition">
                    @for (c of conditions; track c) { <option [value]="c">{{ conditionMeta(c).label }}</option> }
                  </select>
                </div>
              </div>
              <div class="field">
                <label for="item-notes">Notes (optional)</label>
                <textarea id="item-notes" formControlName="notes" maxlength="1000"></textarea>
              </div>
              <button class="btn" type="submit" [disabled]="busy()">Add finding</button>
            </form>
          }
        </section>
      }

      @if (auth.canBook()) {
        <section class="section plain">
          <div class="section-head"><h2>History</h2></div>
          @if (history() === null) {
            <p class="loading">Loading history…</p>
          } @else if (history()!.length === 0) {
            <gf-empty title="No history yet" />
          } @else {
            <ul class="history">
              @for (entry of history()!; track entry.id) {
                <li>
                  <span class="when">{{ dateTime(entry.timestampUtc) }}</span>
                  <span><strong>{{ entry.action }}</strong> by {{ entry.performedBy }}</span>
                  @if (entry.details) { <span class="muted small">{{ entry.details }}</span> }
                </li>
              }
            </ul>
          }
        </section>
      }
    }

    @if (modal() === 'cancel') {
      <gf-modal heading="Cancel this appointment" (closed)="closeModal()">
        <form [formGroup]="cancelForm" (ngSubmit)="submitCancel()" novalidate>
          @if (modalError()) { <div class="banner error" role="alert">{{ modalError() }}</div> }
          <div class="field">
            <label for="reason">Why is it being cancelled?</label>
            <textarea id="reason" formControlName="reason" maxlength="500" [class.invalid]="cancelForm.controls.reason.invalid && cancelForm.controls.reason.touched"></textarea>
            @if (cancelForm.controls.reason.invalid && cancelForm.controls.reason.touched) { <span class="error">Give a reason so the history makes sense later.</span> }
            <span class="hint">Appointments can't be cancelled less than 2 hours before they start.</span>
          </div>
          @if (auth.isAdmin()) {
            <label class="check"><input type="checkbox" formControlName="allowLate"> Allow it even though it is within 2 hours (admin only)</label>
          }
          <div class="btn-row end">
            <button type="button" class="btn ghost" (click)="closeModal()">Keep appointment</button>
            <button type="submit" class="btn danger" [disabled]="busy()">Cancel appointment</button>
          </div>
        </form>
      </gf-modal>
    }

    @if (modal() === 'reschedule') {
      <gf-modal heading="Move to another time" (closed)="closeModal()">
        @if (modalError()) { <div class="banner error" role="alert">{{ modalError() }}</div> }
        <div class="field">
          <label for="resched-date">Day</label>
          <input id="resched-date" type="date" [value]="reschedDate()" (change)="pickRescheduleDate($any($event.target).value)">
        </div>
        @if (slotsLoading()) { <p class="loading">Finding free times…</p> }
        @if (!slotsLoading() && slots().length === 0) {
          <p class="muted">No free times on this day. Try another day.</p>
        }
        @if (slots().length > 0) {
          <gf-slot-picker [slots]="slots()" [selectedUtc]="chosenSlot()?.startUtc ?? ''" [timeZone]="lookups.timeZone()" (chosen)="chosenSlot.set($event)" />
          <p class="hint muted small">The same technician and bay are kept when they are free at the new time.</p>
        }
        @if (auth.isAdmin()) {
          <label class="check late"><input type="checkbox" [checked]="reschedLate()" (change)="reschedLate.set(!reschedLate())"> Allow it even though it is within 2 hours (admin only)</label>
        }
        <div class="btn-row end">
          <button type="button" class="btn ghost" (click)="closeModal()">Close</button>
          <button type="button" class="btn" [disabled]="!chosenSlot() || busy()" (click)="submitReschedule()">Move appointment</button>
        </div>
      </gf-modal>
    }
  `,
  styles: [`
    .steps { list-style: none; display: flex; gap: 0; padding: 0; margin: 0 0 1.5rem; flex-wrap: wrap; }
    .steps li { flex: 1 1 8rem; padding: 0.55rem 0.8rem; border-top: 4px solid var(--line-strong); color: var(--ink-soft); background: var(--panel); display: flex; align-items: center; gap: 0.5rem; font-weight: 500; }
    .steps li[data-state='done'] { border-top-color: var(--ok); color: var(--ink); }
    .steps li[data-state='current'] { border-top-color: var(--signal); color: var(--ink); font-weight: 600; background: var(--signal-wash); }
    .step-no { width: 1.35rem; height: 1.35rem; border-radius: 50%; display: inline-grid; place-items: center; font-size: 0.78rem; background: var(--sunk); font-weight: 600; }
    .steps li[data-state='done'] .step-no { background: var(--ok); color: #fff; }
    .steps li[data-state='current'] .step-no { background: var(--signal); color: var(--signal-ink); }
    .inspect-form { margin-top: 1.25rem; padding-top: 1rem; border-top: 1px solid var(--line); }
    .inspect-form h3 { margin-bottom: 0.75rem; }
    .history { list-style: none; margin: 0; padding: 0; display: grid; gap: 0.1rem; }
    .history li { display: grid; grid-template-columns: 13rem 1fr; gap: 0.1rem 1rem; padding: 0.55rem 0; border-bottom: 1px solid var(--line); }
    .history .small { grid-column: 2; }
    .btn-row.end { justify-content: flex-end; margin-top: 1rem; }
    .late { margin-top: 1rem; }
    @media (max-width: 700px) { .history li { grid-template-columns: 1fr; } .history .small { grid-column: 1; } }
  `]
})
export class AppointmentDetailComponent {
  readonly lookups = inject(LookupService);
  readonly auth = inject(AuthService);
  readonly api = inject(ApiService);
  private readonly toasts = inject(ToastService);
  private readonly fb = inject(FormBuilder);

  readonly conditions = ALL_CONDITIONS;
  readonly appointment = signal<Appointment | null>(null);
  readonly history = signal<AuditLog[] | null>(null);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly busy = signal(false);
  readonly modal = signal<'' | 'cancel' | 'reschedule'>('');
  readonly modalError = signal('');

  readonly reschedDate = signal('');
  readonly reschedLate = signal(false);
  readonly slots = signal<Slot[]>([]);
  readonly slotsLoading = signal(false);
  readonly chosenSlot = signal<Slot | null>(null);

  readonly cancelForm = this.fb.nonNullable.group({
    reason: ['', [Validators.required, Validators.maxLength(500)]],
    allowLate: [false]
  });
  readonly itemForm = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    condition: ['Good' as (typeof ALL_CONDITIONS)[number]],
    notes: ['']
  });

  readonly statusMeta = computed(() => APPOINTMENT_STATUS[this.appointment()?.status ?? 'Booked']);

  readonly steps = computed(() => {
    const current = this.appointment()?.status ?? 'Booked';
    const index = LIFECYCLE.indexOf(current);
    return LIFECYCLE.map((status, i) => ({
      status,
      number: i + 1,
      label: APPOINTMENT_STATUS[status].label,
      state: i < index || current === 'Completed' ? 'done' : i === index ? 'current' : 'todo'
    }));
  });

  readonly noActions = computed(() => {
    const a = this.appointment();
    if (!a) return false;
    if (a.status === 'Booked') return !this.auth.canBook();
    if (a.status === 'CheckedIn') return !this.auth.canInspect() && !this.auth.canBook();
    if (a.status === 'InInspection') return !this.auth.canInspect();
    return true;
  });

  private currentId = '';

  @Input() set id(value: string) {
    this.currentId = value;
    if (value) this.load();
  }

  whenText(): string {
    const a = this.appointment();
    return a ? `${formatDate(a.startTimeUtc, this.lookups.timeZone())}, ${formatTime(a.startTimeUtc, this.lookups.timeZone())} to ${formatTime(a.endTimeUtc, this.lookups.timeZone())}` : '';
  }

  dateTime(value: string): string { return formatDateTime(value, this.lookups.timeZone()); }
  time(value: string): string { return formatTime(value, this.lookups.timeZone()); }
  conditionMeta(c: (typeof ALL_CONDITIONS)[number]) { return CONDITION[c]; }

  /** Runs a status change, shows the result, and refreshes the history. */
  act(successText: string, call: Observable<Appointment>): void {
    this.busy.set(true);
    call.subscribe({
      next: (updated) => {
        this.appointment.set(updated);
        this.busy.set(false);
        this.toasts.success(successText);
        this.loadHistory();
      },
      error: (error: unknown) => {
        this.busy.set(false);
        this.toasts.error(errorMessage(error));
      }
    });
  }

  markNoShow(): void {
    const a = this.appointment();
    if (a) this.act('Marked as a no-show.', this.api.noShow(a.id));
  }

  addItem(): void {
    const a = this.appointment();
    if (!a) return;
    if (this.itemForm.invalid) {
      this.itemForm.markAllAsTouched();
      return;
    }
    const { name, condition, notes } = this.itemForm.getRawValue();
    this.act('Finding recorded.', this.api.addInspectionItem(a.id, { name: name.trim(), condition, notes: notes.trim() || null }));
    this.itemForm.reset({ name: '', condition: 'Good', notes: '' });
  }

  // ---- cancel ----
  submitCancel(): void {
    const a = this.appointment();
    if (!a) return;
    if (this.cancelForm.invalid) {
      this.cancelForm.markAllAsTouched();
      return;
    }
    const { reason, allowLate } = this.cancelForm.getRawValue();
    this.busy.set(true);
    this.modalError.set('');
    this.api.cancel(a.id, reason.trim(), this.auth.isAdmin() && allowLate).subscribe({
      next: (updated) => {
        this.appointment.set(updated);
        this.busy.set(false);
        this.closeModal();
        this.toasts.success('Appointment cancelled.');
        this.loadHistory();
      },
      error: (error: unknown) => {
        this.busy.set(false);
        this.modalError.set(errorMessage(error));
      }
    });
  }

  // ---- reschedule ----
  openReschedule(): void {
    const a = this.appointment();
    if (!a) return;
    this.modal.set('reschedule');
    this.modalError.set('');
    this.reschedLate.set(false);
    this.chosenSlot.set(null);
    this.reschedDate.set(localDateOf(parseUtc(a.startTimeUtc), this.lookups.timeZone()));
    this.loadSlots();
  }

  pickRescheduleDate(value: string): void {
    if (!value) return;
    this.reschedDate.set(value);
    this.chosenSlot.set(null);
    this.loadSlots();
  }

  private loadSlots(): void {
    const a = this.appointment();
    if (!a) return;
    const day = this.reschedDate();
    this.slotsLoading.set(true);
    this.modalError.set('');
    this.api.availability(a.serviceTypeId, day).subscribe({
      next: (result) => {
        if (day !== this.reschedDate()) return;
        this.slots.set(result.slots);
        this.slotsLoading.set(false);
      },
      error: (error: unknown) => {
        this.slots.set([]);
        this.slotsLoading.set(false);
        this.modalError.set(errorMessage(error));
      }
    });
  }

  submitReschedule(): void {
    const a = this.appointment();
    const slot = this.chosenSlot();
    if (!a || !slot) return;
    this.busy.set(true);
    this.modalError.set('');
    this.api.reschedule(a.id, { newStartTimeUtc: slot.startUtc, technicianId: null, bayId: null, allowLate: this.auth.isAdmin() && this.reschedLate() }).subscribe({
      next: (updated) => {
        this.appointment.set(updated);
        this.busy.set(false);
        this.closeModal();
        this.toasts.success('Appointment moved.');
        this.loadHistory();
      },
      error: (error: unknown) => {
        this.busy.set(false);
        this.modalError.set(errorMessage(error));
      }
    });
  }

  closeModal(): void {
    this.modal.set('');
    this.modalError.set('');
    this.cancelForm.reset({ reason: '', allowLate: false });
  }

  // ---- loading ----
  private load(): void {
    const id = this.currentId;
    this.loading.set(true);
    this.error.set('');
    this.appointment.set(null);
    this.history.set(null);
    this.api.appointment(id).subscribe({
      next: (a) => {
        this.lookups.ensureCustomers([a.customerId]).subscribe({
          next: () => this.ready(a),
          error: () => this.ready(a)
        });
      },
      error: (error: unknown) => {
        this.loading.set(false);
        this.error.set(errorMessage(error));
      }
    });
  }

  private ready(a: Appointment): void {
    this.appointment.set(a);
    this.loading.set(false);
    this.loadHistory();
  }

  private loadHistory(): void {
    const a = this.appointment();
    if (!a || !this.auth.canBook()) return;
    this.api.appointmentHistory(a.id).subscribe({
      next: (entries) => this.history.set([...entries].sort((x, y) => y.timestampUtc.localeCompare(x.timestampUtc))),
      error: () => this.history.set([])
    });
  }
}

