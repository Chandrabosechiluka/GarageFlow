import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ConfigService } from './config.service';
import {
  Appointment, AppointmentStatus, AuditLog, Availability, Bay, BookRequest, Customer, InspectionCondition,
  JoinWaitlistRequest, LoginResponse, MaintenanceReport, PagedResult, RescheduleRequest, ServiceType, Skill,
  Technician, UserAccount, UserRole, Vehicle, WaitlistEntry, WaitlistStatus
} from './models';

type Query = Record<string, string | number | boolean | null | undefined>;

function toParams(query: Query): HttpParams {
  let params = new HttpParams();
  for (const [key, value] of Object.entries(query)) {
    if (value !== null && value !== undefined && value !== '') params = params.set(key, String(value));
  }
  return params;
}

/** One method per API endpoint, so screens never build URLs by hand. */
@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly config = inject(ConfigService);

  private url(path: string): string {
    return `${this.config.apiBaseUrl}${path}`;
  }

  // ---- auth and users ----
  login(email: string, password: string): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(this.url('/api/auth/login'), { email, password });
  }
  listUsers(): Observable<UserAccount[]> {
    return this.http.get<UserAccount[]>(this.url('/api/users'));
  }
  createUser(body: { email: string; displayName: string; password: string; role: UserRole }): Observable<UserAccount> {
    return this.http.post<UserAccount>(this.url('/api/users'), body);
  }
  deactivateUser(id: string): Observable<UserAccount> {
    return this.http.post<UserAccount>(this.url(`/api/users/${id}/deactivate`), {});
  }

  // ---- catalog ----
  serviceTypes(): Observable<ServiceType[]> {
    return this.http.get<ServiceType[]>(this.url('/api/service-types'));
  }
  technicians(): Observable<Technician[]> {
    return this.http.get<Technician[]>(this.url('/api/technicians'));
  }
  bays(): Observable<Bay[]> {
    return this.http.get<Bay[]>(this.url('/api/bays'));
  }
  createTechnician(body: { name: string; skills: Skill[] }): Observable<Technician> {
    return this.http.post<Technician>(this.url('/api/technicians'), body);
  }
  createBay(body: { name: string }): Observable<Bay> {
    return this.http.post<Bay>(this.url('/api/bays'), body);
  }
  createServiceType(body: { name: string; durationMinutes: number; requiredSkill: Skill }): Observable<ServiceType> {
    return this.http.post<ServiceType>(this.url('/api/service-types'), body);
  }

  // ---- customers ----
  customers(search: string, page: number, pageSize: number): Observable<PagedResult<Customer>> {
    return this.http.get<PagedResult<Customer>>(this.url('/api/customers'), { params: toParams({ search, page, pageSize }) });
  }
  customer(id: string): Observable<Customer> {
    return this.http.get<Customer>(this.url(`/api/customers/${id}`));
  }
  createCustomer(body: { fullName: string; email: string; phone: string }): Observable<Customer> {
    return this.http.post<Customer>(this.url('/api/customers'), body);
  }
  addVehicle(customerId: string, body: { make: string; model: string; year: number; registrationNumber: string }): Observable<Vehicle> {
    return this.http.post<Vehicle>(this.url(`/api/customers/${customerId}/vehicles`), body);
  }

  // ---- availability and appointments ----
  availability(serviceTypeId: string, date: string, technicianId?: string): Observable<Availability> {
    return this.http.get<Availability>(this.url('/api/availability'), { params: toParams({ serviceTypeId, date, technicianId }) });
  }
  appointments(filter: { fromUtc?: string; toUtc?: string; status?: AppointmentStatus | ''; page: number; pageSize: number }): Observable<PagedResult<Appointment>> {
    return this.http.get<PagedResult<Appointment>>(this.url('/api/appointments'), { params: toParams(filter) });
  }
  appointment(id: string): Observable<Appointment> {
    return this.http.get<Appointment>(this.url(`/api/appointments/${id}`));
  }
  appointmentHistory(id: string): Observable<AuditLog[]> {
    return this.http.get<AuditLog[]>(this.url(`/api/appointments/${id}/history`));
  }
  book(body: BookRequest): Observable<Appointment> {
    return this.http.post<Appointment>(this.url('/api/appointments'), body);
  }
  checkIn(id: string): Observable<Appointment> {
    return this.http.post<Appointment>(this.url(`/api/appointments/${id}/check-in`), {});
  }
  startInspection(id: string): Observable<Appointment> {
    return this.http.post<Appointment>(this.url(`/api/appointments/${id}/start-inspection`), {});
  }
  addInspectionItem(id: string, body: { name: string; condition: InspectionCondition; notes: string | null }): Observable<Appointment> {
    return this.http.post<Appointment>(this.url(`/api/appointments/${id}/inspection-items`), body);
  }
  complete(id: string): Observable<Appointment> {
    return this.http.post<Appointment>(this.url(`/api/appointments/${id}/complete`), {});
  }
  cancel(id: string, reason: string, allowLate: boolean): Observable<Appointment> {
    return this.http.post<Appointment>(this.url(`/api/appointments/${id}/cancel`), { reason, allowLate });
  }
  reschedule(id: string, body: RescheduleRequest): Observable<Appointment> {
    return this.http.post<Appointment>(this.url(`/api/appointments/${id}/reschedule`), body);
  }
  noShow(id: string): Observable<Appointment> {
    return this.http.post<Appointment>(this.url(`/api/appointments/${id}/no-show`), {});
  }

  // ---- waitlist ----
  waitlist(status: WaitlistStatus | ''): Observable<WaitlistEntry[]> {
    return this.http.get<WaitlistEntry[]>(this.url('/api/waitlist'), { params: toParams({ status }) });
  }
  joinWaitlist(body: JoinWaitlistRequest): Observable<WaitlistEntry> {
    return this.http.post<WaitlistEntry>(this.url('/api/waitlist'), body);
  }
  acceptOffer(id: string): Observable<Appointment> {
    return this.http.post<Appointment>(this.url(`/api/waitlist/${id}/accept`), {});
  }
  cancelWaitlist(id: string): Observable<WaitlistEntry> {
    return this.http.post<WaitlistEntry>(this.url(`/api/waitlist/${id}/cancel`), {});
  }

  // ---- admin ----
  audit(filter: { entityName?: string; entityId?: string; performedBy?: string; page: number; pageSize: number }): Observable<PagedResult<AuditLog>> {
    return this.http.get<PagedResult<AuditLog>>(this.url('/api/audit'), { params: toParams(filter) });
  }
  runMaintenance(): Observable<MaintenanceReport> {
    return this.http.post<MaintenanceReport>(this.url('/api/maintenance/run'), {});
  }
  health(): Observable<{ status: string }> {
    return this.http.get<{ status: string }>(this.url('/health'));
  }
  healthDb(): Observable<{ database: string }> {
    return this.http.get<{ database: string }>(this.url('/health/db'));
  }
}
