import { Component, Input, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { errorMessage } from '../../core/api-error';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { LookupService } from '../../core/lookup.service';
import { Customer, Vehicle } from '../../core/models';
import { AddVehicleModalComponent } from '../../shared/customer-modals.component';
import { EmptyComponent } from '../../shared/empty.component';
import { PageHeaderComponent } from '../../shared/page-header.component';

@Component({
  selector: 'gf-customer-detail',
  standalone: true,
  imports: [RouterLink, AddVehicleModalComponent, EmptyComponent, PageHeaderComponent],
  template: `
    @if (loading()) { <p class="loading">Loading the customer…</p> }
    @if (error()) { <div class="banner error" role="alert">{{ error() }} <a routerLink="/customers">Back to customers</a></div> }

    @if (customer(); as c) {
      <gf-page-header [title]="c.fullName" [subtitle]="c.email + ' · ' + c.phone">
        <a class="btn ghost" routerLink="/customers">All customers</a>
        @if (auth.canBook()) { <a class="btn" routerLink="/book" [queryParams]="{ customerId: c.id }">Book an appointment</a> }
      </gf-page-header>

      <section class="section">
        <div class="section-head">
          <h2>Cars</h2>
          @if (auth.canBook()) { <button type="button" class="btn ghost sm" (click)="adding.set(true)">Add a car</button> }
        </div>
        @if (c.vehicles.length === 0) {
          <gf-empty title="No cars on file" text="Add the customer's car so it can be booked in.">
            @if (auth.canBook()) { <button type="button" class="btn" (click)="adding.set(true)">Add a car</button> }
          </gf-empty>
        } @else {
          <div class="table-wrap">
            <table class="data">
              <thead><tr><th>Registration</th><th>Make and model</th><th>Year</th></tr></thead>
              <tbody>
                @for (v of c.vehicles; track v.id) {
                  <tr><td class="nowrap"><strong>{{ v.registrationNumber }}</strong></td><td>{{ v.make }} {{ v.model }}</td><td>{{ v.year }}</td></tr>
                }
              </tbody>
            </table>
          </div>
        }
      </section>

      @if (adding()) {
        <gf-add-vehicle-modal [customerId]="c.id" (added)="onAdded($event)" (closed)="adding.set(false)" />
      }
    }
  `
})
export class CustomerDetailComponent {
  readonly auth = inject(AuthService);
  private readonly api = inject(ApiService);
  private readonly lookups = inject(LookupService);

  readonly customer = signal<Customer | null>(null);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly adding = signal(false);

  @Input() set id(value: string) {
    if (!value) return;
    this.loading.set(true);
    this.error.set('');
    this.customer.set(null);
    this.api.customer(value).subscribe({
      next: (c) => {
        this.lookups.remember(c);
        this.customer.set(c);
        this.loading.set(false);
      },
      error: (error: unknown) => {
        this.loading.set(false);
        this.error.set(errorMessage(error));
      }
    });
  }

  onAdded(vehicle: Vehicle): void {
    this.adding.set(false);
    const c = this.customer();
    if (c) {
      const updated: Customer = { ...c, vehicles: [...c.vehicles, vehicle] };
      this.customer.set(updated);
      this.lookups.remember(updated);
    }
  }
}
