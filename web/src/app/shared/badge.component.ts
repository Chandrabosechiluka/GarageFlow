import { Component, Input } from '@angular/core';
import { Tone } from '../core/status';

/** A small coloured status label. Colour is never the only signal: the words are always there. */
@Component({
  selector: 'gf-badge',
  standalone: true,
  template: `<span class="badge" [attr.data-tone]="tone">{{ label }}</span>`
})
export class BadgeComponent {
  @Input() tone: Tone = 'booked';
  @Input() label = '';
}
