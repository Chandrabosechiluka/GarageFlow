import { Component, EventEmitter, Input, Output } from '@angular/core';
import { Slot } from '../core/models';
import { formatTime } from '../core/tz';

/** A grid of bookable start times. The selected one is outlined; each button says how many technicians are free. */
@Component({
  selector: 'gf-slot-picker',
  standalone: true,
  template: `
    <div class="slots" role="group" aria-label="Available start times">
      @for (slot of slots; track slot.startUtc) {
        <button type="button" class="slot" [class.on]="slot.startUtc === selectedUtc" [attr.aria-pressed]="slot.startUtc === selectedUtc" (click)="chosen.emit(slot)">
          <span class="slot-time">{{ time(slot) }}</span>
          <span class="slot-free">{{ slot.availableTechnicianIds.length }} free</span>
        </button>
      }
    </div>
  `,
  styles: [`
    .slots { display: grid; grid-template-columns: repeat(auto-fill, minmax(6.2rem, 1fr)); gap: 0.5rem; }
    .slot { font: inherit; cursor: pointer; text-align: left; background: #fff; border: 1px solid var(--line-strong); border-radius: 6px; padding: 0.45rem 0.65rem; display: grid; }
    .slot:hover { border-color: var(--petrol); }
    .slot.on { background: var(--petrol); border-color: var(--petrol); color: #fff; }
    .slot-time { font-weight: 600; }
    .slot-free { font-size: 0.78rem; color: var(--ink-soft); }
    .slot.on .slot-free { color: #cfe7e8; }
  `]
})
export class SlotPickerComponent {
  @Input() slots: Slot[] = [];
  @Input() selectedUtc = '';
  @Input() timeZone = 'Asia/Kolkata';
  @Output() chosen = new EventEmitter<Slot>();

  time(slot: Slot): string {
    return formatTime(slot.startUtc, this.timeZone);
  }
}
