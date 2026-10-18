import { Component, Input } from '@angular/core';

/** An empty screen is an invitation to act: say what is missing and what to do about it. */
@Component({
  selector: 'gf-empty',
  standalone: true,
  template: `
    <div class="empty">
      <p class="empty-title">{{ title }}</p>
      @if (text) { <p class="muted">{{ text }}</p> }
      <ng-content></ng-content>
    </div>
  `
})
export class EmptyComponent {
  @Input() title = '';
  @Input() text = '';
}
