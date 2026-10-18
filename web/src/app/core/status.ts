import { AppointmentStatus, InspectionCondition, WaitlistStatus } from './models';

export type Tone = 'booked' | 'checked' | 'inspect' | 'done' | 'off' | 'alert' | 'wait' | 'offer';

export const APPOINTMENT_STATUS: Record<AppointmentStatus, { label: string; tone: Tone }> = {
  Booked: { label: 'Booked', tone: 'booked' },
  CheckedIn: { label: 'Checked in', tone: 'checked' },
  InInspection: { label: 'In inspection', tone: 'inspect' },
  Completed: { label: 'Completed', tone: 'done' },
  Cancelled: { label: 'Cancelled', tone: 'off' },
  NoShow: { label: 'No-show', tone: 'alert' }
};

export const WAITLIST_STATUS: Record<WaitlistStatus, { label: string; tone: Tone }> = {
  Waiting: { label: 'Waiting', tone: 'wait' },
  Offered: { label: 'Offer sent', tone: 'offer' },
  Accepting: { label: 'Accepting', tone: 'inspect' },
  Fulfilled: { label: 'Booked', tone: 'done' },
  Cancelled: { label: 'Cancelled', tone: 'off' },
  Expired: { label: 'Expired', tone: 'off' }
};

export const CONDITION: Record<InspectionCondition, { label: string; tone: Tone }> = {
  Good: { label: 'Good', tone: 'done' },
  NeedsAttention: { label: 'Needs attention', tone: 'inspect' },
  Critical: { label: 'Critical', tone: 'alert' }
};

/** The path an appointment follows when everything goes well. */
export const LIFECYCLE: AppointmentStatus[] = ['Booked', 'CheckedIn', 'InInspection', 'Completed'];

export function skillLabel(skill: string): string {
  return skill.replace(/([a-z])([A-Z])/g, '$1 $2');
}
