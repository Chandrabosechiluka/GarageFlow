// Shapes of the data the API sends and receives. They mirror the C# contracts in GarageFlow.Application/Contracts.
// Enums travel as text ("Booked"), dates as ISO strings ("2026-11-03T09:00:00Z" is UTC).

export type AppointmentStatus = 'Booked' | 'CheckedIn' | 'InInspection' | 'Completed' | 'Cancelled' | 'NoShow';
export type InspectionCondition = 'Good' | 'NeedsAttention' | 'Critical';
export type Skill = 'General' | 'OilChange' | 'Brakes' | 'Tires' | 'Engine' | 'Electrical';
export type UserRole = 'Admin' | 'Advisor' | 'Technician';
export type WaitlistStatus = 'Waiting' | 'Offered' | 'Accepting' | 'Fulfilled' | 'Cancelled' | 'Expired';

export const ALL_SKILLS: Skill[] = ['General', 'OilChange', 'Brakes', 'Tires', 'Engine', 'Electrical'];
export const ALL_ROLES: UserRole[] = ['Admin', 'Advisor', 'Technician'];
export const ALL_CONDITIONS: InspectionCondition[] = ['Good', 'NeedsAttention', 'Critical'];
export const ALL_APPOINTMENT_STATUSES: AppointmentStatus[] = ['Booked', 'CheckedIn', 'InInspection', 'Completed', 'Cancelled', 'NoShow'];
export const ALL_WAITLIST_STATUSES: WaitlistStatus[] = ['Waiting', 'Offered', 'Accepting', 'Fulfilled', 'Cancelled', 'Expired'];

export interface LoginResponse {
  accessToken: string;
  expiresAtUtc: string;
  role: UserRole;
  displayName: string;
  tokenType: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
}

export interface Vehicle {
  id: string;
  customerId: string;
  make: string;
  model: string;
  year: number;
  registrationNumber: string;
}

export interface Customer {
  id: string;
  fullName: string;
  email: string;
  phone: string;
  vehicles: Vehicle[];
}

export interface ServiceType {
  id: string;
  name: string;
  durationMinutes: number;
  requiredSkill: Skill;
}

export interface Technician {
  id: string;
  shopId: string;
  name: string;
  skills: Skill[];
}

export interface Bay {
  id: string;
  shopId: string;
  name: string;
}

export interface InspectionItem {
  id: string;
  name: string;
  condition: InspectionCondition;
  notes: string | null;
}

export interface Appointment {
  id: string;
  shopId: string;
  customerId: string;
  vehicleId: string;
  serviceTypeId: string;
  technicianId: string;
  bayId: string;
  startTimeUtc: string;
  endTimeUtc: string;
  status: AppointmentStatus;
  cancellationReason: string | null;
  inspectionItems: InspectionItem[];
}

export interface Slot {
  startUtc: string;
  endUtc: string;
  startLocal: string;
  endLocal: string;
  suggestedTechnicianId: string;
  suggestedBayId: string;
  availableTechnicianIds: string[];
  availableBayIds: string[];
}

export interface Availability {
  date: string;
  shopId: string;
  timeZoneId: string;
  serviceTypeId: string;
  durationMinutes: number;
  slots: Slot[];
}

export interface WaitlistEntry {
  id: string;
  shopId: string;
  customerId: string;
  vehicleId: string;
  serviceTypeId: string;
  earliestStartUtc: string;
  latestStartUtc: string;
  status: WaitlistStatus;
  offeredStartUtc: string | null;
  offerExpiresUtc: string | null;
  appointmentId: string | null;
  createdUtc: string;
}

export interface AuditLog {
  id: string;
  entityName: string;
  entityId: string;
  action: string;
  performedBy: string;
  timestampUtc: string;
  details: string | null;
}

export interface UserAccount {
  id: string;
  email: string;
  displayName: string;
  role: UserRole;
  isActive: boolean;
}

export interface MaintenanceReport {
  noShowsMarked: number;
  remindersSent: number;
  offersNotified: number;
  offersExpired: number;
  total: number;
}

// ---- requests ----
export interface BookRequest {
  customerId: string;
  vehicleId: string;
  serviceTypeId: string;
  technicianId: string | null;
  bayId: string | null;
  startTimeUtc: string;
}

export interface RescheduleRequest {
  newStartTimeUtc: string;
  technicianId: string | null;
  bayId: string | null;
  allowLate: boolean;
}

export interface JoinWaitlistRequest {
  customerId: string;
  vehicleId: string;
  serviceTypeId: string;
  earliestStartUtc: string;
  latestStartUtc: string;
}
