import { Component, Input, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { errorMessage } from '../../core/api-error';
import { ApiService } from '../../core/api.service';
import { LookupService, vehicleText } from '../../core/lookup.service';
import { Customer, Slot, Vehicle } from '../../core/models';
import { skillLabel } from '../../core/status';
import { ToastService } from '../../core/toast.service';
import { formatDate, formatTime, localInputToUtc, todayIn } from '../../core/tz';
import { AddVehicleModalComponent, NewCustomerModalComponent } from '../../shared/customer-modals.component';
import { ModalComponent } from '../../shared/modal.component';
import { PageHeaderComponent } from '../../shared/page-header.component';
import { SlotPickerComponent } from '../../shared/slot-picker.component';

@Component({
  selector: 'gf-booking',
  standalone: true,
  imports: [RouterLink, PageHeaderComponent, SlotPickerComponent, ModalComponent, NewCustomerModalComponent, AddVehicleModalComponent],
  template: `
    <gf-page-header title="New booking" subtitle="Choose the customer and car, the service, then a free time." />

    <div class="booking">
      <div class="steps-col">
        <!-- 1: customer and car -->
        <section class="section">
          <h2><span class="n">1</span> Customer and car</h2>

          @if (customer(); as c) {
            <div class="picked">
              <div>
                <strong>{{ c.fullName }}</strong>
                <p class="muted small">{{ c.email }} · {{ c.phone }}</p>
              </div>
              <button type="button" class="btn ghost sm" (click)="clearCustomer()">Change</button>
            </div>

            @if (c.vehicles.length === 0) {
              <p class="muted">This customer has no car on file yet.</p>
            } @else {
              <div class="choices" role="radiogroup" aria-label="Car">
                @for (v of c.vehicles; track v.id) {
                  <label class="choice" [class.on]="vehicleId() === v.id">
                    <input type="radio" name="vehicle" [checked]="vehicleId() === v.id" (change)="vehicleId.set(v.id)">
                    <span>{{ vehicleLabel(v) }} <span class="muted small">({{ v.year }})</span></span>
                  </label>
                }
              </div>
            }
            <button type="button" class="btn ghost sm" (click)="addingVehicle.set(true)">Add another car</button>
          } @else {
            <div class="field">
              <label for="cust-search">Find a customer</label>
              <input id="cust-search" type="search" placeholder="Name, email or phone" [value]="query()" (input)="onSearch($any($event.target).value)">
            </div>
            @if (searching()) { <p class="loading">Searching…</p> }
            <ul class="results">
              @for (c of results(); track c.id) {
                <li><button type="button" class="result" (click)="selectCustomer(c)"><strong>{{ c.fullName }}</strong><span class="muted small">{{ c.email }} · {{ c.phone }} · {{ c.vehicles.length }} {{ c.vehicles.length === 1 ? 'car' : 'cars' }}</span></button></li>
              }
            </ul>
            @if (!searching() && results().length === 0) {
              <p class="muted">No customer found{{ query() ? ' for "' + query() + '"' : '' }}.</p>
            }
            <button type="button" class="btn ghost" (click)="newCustomer.set(true)">New customer</button>
          }
        </section>

        <!-- 2: service -->
        <section class="section">
          <h2><span class="n">2</span> Service</h2>
          @if (lookups.serviceTypes().length === 0) {
            <p class="muted">No services are set up yet. An admin can add them in Shop setup.</p>
          } @else {
            <div class="choices" role="radiogroup" aria-label="Service">
              @for (s of lookups.serviceTypes(); track s.id) {
                <label class="choice" [class.on]="serviceId() === s.id">
                  <input type="radio" name="service" [checked]="serviceId() === s.id" (change)="selectService(s.id)">
                  <span>{{ s.name }} <span class="muted small">{{ s.durationMinutes }} min · needs {{ skill(s.requiredSkill) }}</span></span>
                </label>
              }
            </div>
          }
        </section>

        <!-- 3: time -->
        <section class="section">
          <h2><span class="n">3</span> Day and time</h2>
          <div class="field day-field">
            <label for="book-date">Day</label>
            <input id="book-date" type="date" [min]="today()" [value]="date()" (change)="pickDate($any($event.target).value)">
          </div>

          @if (!serviceId()) {
            <p class="muted">Choose a service to see its free times.</p>
          } @else {
            @if (slotsLoading()) { <p class="loading">Finding free times…</p> }
            @if (slotsError()) { <div class="banner error" role="alert">{{ slotsError() }}</div> }
            @if (!slotsLoading() && !slotsError() && slots().length === 0) {
              <div class="banner info">
                No free times on {{ dayText() }}. Try another day, or put the customer on the waitlist and they'll be offered the first slot that opens.
              </div>
              <button type="button" class="btn ghost" [disabled]="!customer() || !vehicleId()" (click)="openWaitlist()">Join the waitlist</button>
              @if (!customer() || !vehicleId()) { <p class="muted small">Pick the customer and car first.</p> }
            }
            @if (slots().length > 0) {
              <gf-slot-picker [slots]="slots()" [selectedUtc]="slot()?.startUtc ?? ''" [timeZone]="lookups.timeZone()" (chosen)="selectSlot($event)" />
              @if (slot(); as s) {
                <div class="form-row assign">
                  <div class="field">
                    <label for="tech">Technician</label>
                    <select id="tech" (change)="techId.set($any($event.target).value)">
                      <option value="" [selected]="!techId()">Assign automatically ({{ lookups.technicianName(s.suggestedTechnicianId) }})</option>
                      @for (id of s.availableTechnicianIds; track id) { <option [value]="id" [selected]="techId() === id">{{ lookups.technicianName(id) }}</option> }
                    </select>
                  </div>
                  <div class="field">
                    <label for="bay">Bay</label>
                    <select id="bay" (change)="bayId.set($any($event.target).value)">
                      <option value="" [selected]="!bayId()">Assign automatically ({{ lookups.bayName(s.suggestedBayId) }})</option>
                      @for (id of s.availableBayIds; track id) { <option [value]="id" [selected]="bayId() === id">{{ lookups.bayName(id) }}</option> }
                    </select>
                  </div>
                </div>
              }
            }
          }
        </section>
      </div>

      <aside class="summary">
        <h2>Summary</h2>
        <dl class="facts">
          <dt>Customer</dt><dd>{{ customer()?.fullName || 'Not chosen' }}</dd>
          <dt>Car</dt><dd>{{ chosenVehicleText() }}</dd>
          <dt>Service</dt><dd>{{ chosenServiceText() }}</dd>
          <dt>When</dt><dd>{{ chosenWhenText() }}</dd>
        </dl>
        @if (bookError()) { <div class="banner error" role="alert">{{ bookError() }}</div> }
        <button type="button" class="btn full" [disabled]="!ready() || booking()" (click)="book()">{{ booking() ? 'Booking…' : 'Book appointment' }}</button>
        @if (!ready()) { <p class="muted small">{{ missingText() }}</p> }
        <a class="link-btn small" routerLink="/">Cancel and go back</a>
      </aside>
    </div>

    @if (newCustomer()) {
      <gf-new-customer-modal (created)="onCustomerCreated($event)" (closed)="newCustomer.set(false)" />
    }
    @if (addingVehicle() && customer(); as c) {
      <gf-add-vehicle-modal [customerId]="c.id" (added)="onVehicleAdded($event)" (closed)="addingVehicle.set(false)" />
    }
    @if (waitlistOpen()) {
      <gf-modal heading="Join the waitlist" (closed)="waitlistOpen.set(false)">
        <p class="muted">We'll offer {{ customer()?.fullName }} the first slot that opens up within this window. Times are in the shop's time.</p>
        @if (waitlistError()) { <div class="banner error" role="alert">{{ waitlistError() }}</div> }
        <div class="form-row">
          <div class="field"><label for="wl-from">Earliest start</label><input id="wl-from" type="datetime-local" [value]="wlFrom()" (input)="wlFrom.set($any($event.target).value)"></div>
          <div class="field"><label for="wl-to">Latest start</label><input id="wl-to" type="datetime-local" [value]="wlTo()" (input)="wlTo.set($any($event.target).value)"></div>
        </div>
        <div class="btn-row end">
          <button type="button" class="btn ghost" (click)="waitlistOpen.set(false)">Cancel</button>
          <button type="button" class="btn" [disabled]="booking()" (click)="joinWaitlist()">Join the waitlist</button>
        </div>
      </gf-modal>
    }
  `,
  styles: [`
    .booking { display: grid; grid-template-columns: minmax(0, 1fr) 20rem; gap: 1.5rem; align-items: start; }
    .steps-col { min-width: 0; }
    .section h2 { display: flex; align-items: center; gap: 0.6rem; margin-bottom: 0.9rem; }
    .n { width: 1.6rem; height: 1.6rem; border-radius: 50%; background: var(--ink); color: #fff; display: inline-grid; place-items: center; font-size: 0.85rem; }
    .picked { display: flex; justify-content: space-between; align-items: center; gap: 1rem; padding: 0.7rem 0.9rem; background: var(--petrol-wash); border-radius: 6px; margin-bottom: 0.9rem; }
    .choices { display: grid; gap: 0.4rem; margin-bottom: 0.75rem; }
    .choice { display: flex; align-items: center; gap: 0.6rem; padding: 0.55rem 0.8rem; border: 1px solid var(--line-strong); border-radius: 6px; background: #fff; cursor: pointer; }
    .choice.on { border-color: var(--petrol); background: var(--petrol-wash); }
    .choice input { width: auto; margin: 0; }
    .results { list-style: none; padding: 0; margin: 0 0 0.9rem; display: grid; gap: 0.35rem; }
    .result { width: 100%; text-align: left; font: inherit; display: grid; gap: 0.1rem; padding: 0.55rem 0.8rem; border: 1px solid var(--line); border-radius: 6px; background: #fff; cursor: pointer; }
    .result:hover { border-color: var(--petrol); }
    .day-field { max-width: 14rem; }
    .assign { margin-top: 1rem; }
    .summary { position: sticky; top: 1rem; background: var(--panel); border-top: 3px solid var(--signal); padding: 1.1rem 1.25rem 1.25rem; display: grid; gap: 0.9rem; }
    .summary .btn.full { width: 100%; }
    .btn-row.end { justify-content: flex-end; margin-top: 1rem; }
    @media (max-width: 1000px) { .booking { grid-template-columns: 1fr; } .summary { position: static; } }
  `]
})
export class BookingComponent implements OnInit, OnDestroy {
  readonly lookups = inject(LookupService);
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly toasts = inject(ToastService);

  // customer and car
  readonly query = signal('');
  readonly results = signal<Customer[]>([]);
  readonly searching = signal(false);
  readonly customer = signal<Customer | null>(null);
  readonly vehicleId = signal('');
  readonly newCustomer = signal(false);
  readonly addingVehicle = signal(false);

  // service and time
  readonly serviceId = signal('');
  readonly date = signal(todayIn(this.lookups.timeZone()));
  readonly slots = signal<Slot[]>([]);
  readonly slot = signal<Slot | null>(null);
  readonly slotsLoading = signal(false);
  readonly slotsError = signal('');
  readonly techId = signal('');
  readonly bayId = signal('');

  // saving
  readonly booking = signal(false);
  readonly bookError = signal('');

  // waitlist
  readonly waitlistOpen = signal(false);
  readonly waitlistError = signal('');
  readonly wlFrom = signal('');
  readonly wlTo = signal('');

  private searchTimer: ReturnType<typeof setTimeout> | undefined;

  readonly today = computed(() => todayIn(this.lookups.timeZone()));
  readonly ready = computed(() => !!this.customer() && !!this.vehicleId() && !!this.serviceId() && !!this.slot());

  @Input() set customerId(value: string | undefined) {
    if (value) {
      this.api.customer(value).subscribe({
        next: (c) => this.selectCustomer(c),
        error: (error: unknown) => this.toasts.error(errorMessage(error))
      });
    }
  }

  ngOnInit(): void {
    this.search();
  }

  ngOnDestroy(): void {
    clearTimeout(this.searchTimer);
  }

  skill(value: string): string { return skillLabel(value); }
  vehicleLabel(v: Vehicle): string { return vehicleText(v); }

  chosenVehicleText(): string {
    const v = this.customer()?.vehicles.find((x) => x.id === this.vehicleId());
    return v ? vehicleText(v) : 'Not chosen';
  }

  chosenServiceText(): string {
    const s = this.lookups.serviceById().get(this.serviceId());
    return s ? `${s.name} (${s.durationMinutes} min)` : 'Not chosen';
  }

  chosenWhenText(): string {
    const s = this.slot();
    return s ? `${formatDate(s.startUtc, this.lookups.timeZone())}, ${formatTime(s.startUtc, this.lookups.timeZone())}` : 'Not chosen';
  }

  dayText(): string {
    return formatDate(new Date(`${this.date()}T12:00:00Z`), this.lookups.timeZone());
  }

  missingText(): string {
    if (!this.customer()) return 'Choose a customer to continue.';
    if (!this.vehicleId()) return 'Choose the car.';
    if (!this.serviceId()) return 'Choose a service.';
    return 'Choose a time.';
  }

  // ---- customer ----
  onSearch(value: string): void {
    this.query.set(value);
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.search(), 250);
  }

  private search(): void {
    this.searching.set(true);
    this.api.customers(this.query().trim(), 1, 8).subscribe({
      next: (page) => {
        this.results.set(page.items);
        this.searching.set(false);
      },
      error: (error: unknown) => {
        this.searching.set(false);
        this.toasts.error(errorMessage(error));
      }
    });
  }

  selectCustomer(c: Customer): void {
    this.customer.set(c);
    this.lookups.remember(c);
    this.vehicleId.set(c.vehicles.length === 1 ? c.vehicles[0].id : '');
  }

  clearCustomer(): void {
    this.customer.set(null);
    this.vehicleId.set('');
    this.query.set('');
    this.search();
  }

  onCustomerCreated(c: Customer): void {
    this.newCustomer.set(false);
    this.selectCustomer(c);
  }

  onVehicleAdded(v: Vehicle): void {
    this.addingVehicle.set(false);
    const c = this.customer();
    if (c) {
      const updated: Customer = { ...c, vehicles: [...c.vehicles, v] };
      this.customer.set(updated);
      this.lookups.remember(updated);
      this.vehicleId.set(v.id);
    }
  }

  // ---- service and time ----
  selectService(id: string): void {
    this.serviceId.set(id);
    this.loadSlots();
  }

  pickDate(value: string): void {
    if (!value) return;
    this.date.set(value);
    this.loadSlots();
  }

  selectSlot(slot: Slot): void {
    this.slot.set(slot);
    this.techId.set('');
    this.bayId.set('');
    this.bookError.set('');
  }

  private loadSlots(): void {
    const serviceId = this.serviceId();
    const day = this.date();
    this.slot.set(null);
    this.slots.set([]);
    this.slotsError.set('');
    if (!serviceId) return;
    this.slotsLoading.set(true);
    this.api.availability(serviceId, day).subscribe({
      next: (result) => {
        if (serviceId !== this.serviceId() || day !== this.date()) return; // an older answer arrived late
        this.slots.set(result.slots);
        this.slotsLoading.set(false);
      },
      error: (error: unknown) => {
        this.slotsLoading.set(false);
        this.slotsError.set(errorMessage(error));
      }
    });
  }

  // ---- book ----
  book(): void {
    const c = this.customer();
    const s = this.slot();
    if (!c || !s || !this.vehicleId() || !this.serviceId()) return;
    this.booking.set(true);
    this.bookError.set('');
    this.api.book({
      customerId: c.id,
      vehicleId: this.vehicleId(),
      serviceTypeId: this.serviceId(),
      technicianId: this.techId() || null,
      bayId: this.bayId() || null,
      startTimeUtc: s.startUtc
    }).subscribe({
      next: (appointment) => {
        this.booking.set(false);
        this.toasts.success('Appointment booked.');
        void this.router.navigate(['/appointments', appointment.id]);
      },
      error: (error: unknown) => {
        this.booking.set(false);
        this.bookError.set(errorMessage(error));
        this.loadSlots(); // someone may have taken the slot a moment ago: show what is free now
      }
    });
  }

  // ---- waitlist ----
  openWaitlist(): void {
    this.waitlistError.set('');
    this.wlFrom.set(`${this.date()}T09:00`);
    this.wlTo.set(`${this.date()}T17:00`);
    this.waitlistOpen.set(true);
  }

  joinWaitlist(): void {
    const c = this.customer();
    if (!c || !this.vehicleId() || !this.serviceId()) return;
    const zone = this.lookups.timeZone();
    const earliest = localInputToUtc(this.wlFrom(), zone);
    const latest = localInputToUtc(this.wlTo(), zone);
    if (isNaN(earliest.getTime()) || isNaN(latest.getTime()) || latest <= earliest) {
      this.waitlistError.set('The latest start must be after the earliest start.');
      return;
    }
    this.booking.set(true);
    this.api.joinWaitlist({
      customerId: c.id,
      vehicleId: this.vehicleId(),
      serviceTypeId: this.serviceId(),
      earliestStartUtc: earliest.toISOString(),
      latestStartUtc: latest.toISOString()
    }).subscribe({
      next: () => {
        this.booking.set(false);
        this.waitlistOpen.set(false);
        this.toasts.success('Added to the waitlist.');
        void this.router.navigate(['/waitlist']);
      },
      error: (error: unknown) => {
        this.booking.set(false);
        this.waitlistError.set(errorMessage(error));
      }
    });
  }
}
