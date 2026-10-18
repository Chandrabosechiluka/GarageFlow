import { Component, EventEmitter, Input, Output, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { errorMessage } from '../core/api-error';
import { ApiService } from '../core/api.service';
import { Customer, Vehicle } from '../core/models';
import { ToastService } from '../core/toast.service';
import { ModalComponent } from './modal.component';

/** Adds a customer, and optionally their first car in the same step. */
@Component({
  selector: 'gf-new-customer-modal',
  standalone: true,
  imports: [ReactiveFormsModule, ModalComponent],
  template: `
    <gf-modal heading="New customer" (closed)="closed.emit()">
      <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
        @if (error()) { <div class="banner error" role="alert">{{ error() }}</div> }
        <div class="field">
          <label for="nc-name">Full name</label>
          <input id="nc-name" type="text" formControlName="fullName" maxlength="200" [class.invalid]="bad('fullName')">
          @if (bad('fullName')) { <span class="error">Enter the customer's name.</span> }
        </div>
        <div class="form-row">
          <div class="field">
            <label for="nc-email">Email</label>
            <input id="nc-email" type="email" formControlName="email" maxlength="256" [class.invalid]="bad('email')">
            @if (bad('email')) { <span class="error">Enter a valid email address.</span> }
          </div>
          <div class="field">
            <label for="nc-phone">Phone</label>
            <input id="nc-phone" type="text" formControlName="phone" maxlength="30" [class.invalid]="bad('phone')">
            @if (bad('phone')) { <span class="error">Enter a phone number.</span> }
          </div>
        </div>

        <h3 class="sub">Car (optional, you can add it later)</h3>
        <div class="form-row">
          <div class="field"><label for="nc-make">Make</label><input id="nc-make" type="text" formControlName="make" maxlength="100" placeholder="Maruti"></div>
          <div class="field"><label for="nc-model">Model</label><input id="nc-model" type="text" formControlName="model" maxlength="100" placeholder="Swift"></div>
        </div>
        <div class="form-row">
          <div class="field"><label for="nc-year">Year</label><input id="nc-year" type="number" formControlName="year" min="1980" max="2100"></div>
          <div class="field"><label for="nc-reg">Registration number</label><input id="nc-reg" type="text" formControlName="registration" maxlength="20" placeholder="TS09AB1234"></div>
        </div>
        @if (carError()) { <p class="error-text">{{ carError() }}</p> }

        <div class="btn-row end">
          <button type="button" class="btn ghost" (click)="closed.emit()">Cancel</button>
          <button type="submit" class="btn" [disabled]="busy()">{{ busy() ? 'Saving…' : 'Save customer' }}</button>
        </div>
      </form>
    </gf-modal>
  `,
  styles: [`
    .sub { margin: 0.5rem 0 0.75rem; }
    .btn-row.end { justify-content: flex-end; margin-top: 0.75rem; }
    .error-text { color: var(--danger); font-size: 0.86rem; margin-bottom: 0.5rem; }
  `]
})
export class NewCustomerModalComponent {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(ApiService);
  private readonly toasts = inject(ToastService);

  @Output() created = new EventEmitter<Customer>();
  @Output() closed = new EventEmitter<void>();

  readonly busy = signal(false);
  readonly error = signal('');
  readonly carError = signal('');

  readonly form = this.fb.nonNullable.group({
    fullName: ['', [Validators.required, Validators.maxLength(200)]],
    email: ['', [Validators.required, Validators.email]],
    phone: ['', [Validators.required, Validators.maxLength(30)]],
    make: [''],
    model: [''],
    year: [new Date().getFullYear()],
    registration: ['']
  });

  bad(name: 'fullName' | 'email' | 'phone'): boolean {
    const control = this.form.controls[name];
    return control.invalid && control.touched;
  }

  submit(): void {
    this.error.set('');
    this.carError.set('');
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const anyCar = [v.make, v.model, v.registration].some((x) => x.trim() !== '');
    const fullCar = [v.make, v.model, v.registration].every((x) => x.trim() !== '');
    if (anyCar && !fullCar) {
      this.carError.set('Fill in make, model and registration for the car, or leave all three empty.');
      return;
    }

    this.busy.set(true);
    this.api.createCustomer({ fullName: v.fullName.trim(), email: v.email.trim(), phone: v.phone.trim() }).subscribe({
      next: (customer) => {
        if (!anyCar) {
          this.finish(customer);
        } else {
          this.api.addVehicle(customer.id, { make: v.make.trim(), model: v.model.trim(), year: Number(v.year), registrationNumber: v.registration.trim() }).subscribe({
            next: () => this.api.customer(customer.id).subscribe({ next: (full) => this.finish(full), error: () => this.finish(customer) }),
            error: (error: unknown) => {
              // The customer exists now; say so, so the person does not create them twice.
              this.toasts.error(`Customer saved, but the car was not: ${errorMessage(error)}`);
              this.finish(customer);
            }
          });
        }
      },
      error: (error: unknown) => {
        this.busy.set(false);
        this.error.set(errorMessage(error));
      }
    });
  }

  private finish(customer: Customer): void {
    this.busy.set(false);
    this.toasts.success('Customer saved.');
    this.created.emit(customer);
  }
}

/** Adds another car to an existing customer. */
@Component({
  selector: 'gf-add-vehicle-modal',
  standalone: true,
  imports: [ReactiveFormsModule, ModalComponent],
  template: `
    <gf-modal heading="Add a car" (closed)="closed.emit()">
      <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
        @if (error()) { <div class="banner error" role="alert">{{ error() }}</div> }
        <div class="form-row">
          <div class="field"><label for="av-make">Make</label><input id="av-make" type="text" formControlName="make" maxlength="100" [class.invalid]="bad('make')"></div>
          <div class="field"><label for="av-model">Model</label><input id="av-model" type="text" formControlName="model" maxlength="100" [class.invalid]="bad('model')"></div>
        </div>
        <div class="form-row">
          <div class="field"><label for="av-year">Year</label><input id="av-year" type="number" formControlName="year" min="1980" max="2100" [class.invalid]="bad('year')"></div>
          <div class="field"><label for="av-reg">Registration number</label><input id="av-reg" type="text" formControlName="registration" maxlength="20" [class.invalid]="bad('registration')"></div>
        </div>
        @if (form.invalid && form.touched) { <p class="error-text">Fill in every field. The year must be between 1980 and 2100.</p> }
        <div class="btn-row end">
          <button type="button" class="btn ghost" (click)="closed.emit()">Cancel</button>
          <button type="submit" class="btn" [disabled]="busy()">{{ busy() ? 'Saving…' : 'Add car' }}</button>
        </div>
      </form>
    </gf-modal>
  `,
  styles: [`
    .btn-row.end { justify-content: flex-end; margin-top: 0.75rem; }
    .error-text { color: var(--danger); font-size: 0.86rem; }
  `]
})
export class AddVehicleModalComponent {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(ApiService);
  private readonly toasts = inject(ToastService);

  @Input() customerId = '';
  @Output() added = new EventEmitter<Vehicle>();
  @Output() closed = new EventEmitter<void>();

  readonly busy = signal(false);
  readonly error = signal('');

  readonly form = this.fb.nonNullable.group({
    make: ['', [Validators.required, Validators.maxLength(100)]],
    model: ['', [Validators.required, Validators.maxLength(100)]],
    year: [new Date().getFullYear(), [Validators.required, Validators.min(1980), Validators.max(2100)]],
    registration: ['', [Validators.required, Validators.maxLength(20)]]
  });

  bad(name: 'make' | 'model' | 'year' | 'registration'): boolean {
    const control = this.form.controls[name];
    return control.invalid && control.touched;
  }

  submit(): void {
    this.error.set('');
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.busy.set(true);
    this.api.addVehicle(this.customerId, { make: v.make.trim(), model: v.model.trim(), year: Number(v.year), registrationNumber: v.registration.trim() }).subscribe({
      next: (vehicle) => {
        this.busy.set(false);
        this.toasts.success('Car added.');
        this.added.emit(vehicle);
      },
      error: (error: unknown) => {
        this.busy.set(false);
        this.error.set(errorMessage(error));
      }
    });
  }
}
