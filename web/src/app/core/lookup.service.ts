import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, forkJoin, map, of, tap } from 'rxjs';
import { ApiService } from './api.service';
import { Bay, Customer, ServiceType, Technician, Vehicle } from './models';
import { DEFAULT_TIME_ZONE, todayIn } from './tz';

/**
 * Reference data that almost every screen needs: services, technicians, bays, the shop's time zone, and a small
 * cache of customers (appointments only carry ids, screens want names).
 */
@Injectable({ providedIn: 'root' })
export class LookupService {
  private readonly api = inject(ApiService);

  readonly serviceTypes = signal<ServiceType[]>([]);
  readonly technicians = signal<Technician[]>([]);
  readonly bays = signal<Bay[]>([]);
  readonly timeZone = signal<string>(DEFAULT_TIME_ZONE);
  readonly loaded = signal(false);

  private readonly customerCache = new Map<string, Customer>();

  readonly serviceById = computed(() => new Map(this.serviceTypes().map((s) => [s.id, s])));
  readonly technicianById = computed(() => new Map(this.technicians().map((t) => [t.id, t])));
  readonly bayById = computed(() => new Map(this.bays().map((b) => [b.id, b])));

  /** Loads the reference data once per sign-in (call again after the Admin changes the shop setup). */
  load(force = false): Observable<void> {
    if (this.loaded() && !force) return of(undefined);
    return forkJoin({
      services: this.api.serviceTypes(),
      technicians: this.api.technicians(),
      bays: this.api.bays()
    }).pipe(
      tap(({ services, technicians, bays }) => {
        this.serviceTypes.set(services);
        this.technicians.set(technicians);
        this.bays.set(bays);
        this.loaded.set(true);
        // The availability answer is the only place the API tells us the shop's time zone.
        if (services.length > 0) {
          this.api.availability(services[0].id, todayIn(this.timeZone())).subscribe({
            next: (a) => this.timeZone.set(a.timeZoneId || DEFAULT_TIME_ZONE),
            error: () => undefined
          });
        }
      }),
      map(() => undefined)
    );
  }

  reset(): void {
    this.loaded.set(false);
    this.serviceTypes.set([]);
    this.technicians.set([]);
    this.bays.set([]);
    this.customerCache.clear();
  }

  serviceName(id: string): string { return this.serviceById().get(id)?.name ?? 'Service'; }
  technicianName(id: string): string { return this.technicianById().get(id)?.name ?? 'Technician'; }
  bayName(id: string): string { return this.bayById().get(id)?.name ?? 'Bay'; }

  cachedCustomer(id: string): Customer | undefined { return this.customerCache.get(id); }

  customerName(id: string): string { return this.customerCache.get(id)?.fullName ?? '…'; }

  vehicleLabel(customerId: string, vehicleId: string): string {
    const vehicle = this.customerCache.get(customerId)?.vehicles.find((v) => v.id === vehicleId);
    return vehicle ? vehicleText(vehicle) : '…';
  }

  remember(customer: Customer): void { this.customerCache.set(customer.id, customer); }

  /** Makes sure these customers are in the cache (one request per unknown id, in parallel). */
  ensureCustomers(ids: string[]): Observable<void> {
    const missing = [...new Set(ids)].filter((id) => !this.customerCache.has(id));
    if (missing.length === 0) return of(undefined);
    return forkJoin(missing.map((id) => this.api.customer(id))).pipe(
      tap((customers) => customers.forEach((c) => this.customerCache.set(c.id, c))),
      map(() => undefined)
    );
  }
}

export function vehicleText(vehicle: Vehicle): string {
  return `${vehicle.make} ${vehicle.model} · ${vehicle.registrationNumber}`;
}
