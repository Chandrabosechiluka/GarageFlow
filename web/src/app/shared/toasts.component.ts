import { Component, inject } from '@angular/core';
import { ToastService } from '../core/toast.service';

@Component({
  selector: 'gf-toasts',
  standalone: true,
  template: `
    <div class="toasts" aria-live="polite">
      @for (toast of toasts.toasts(); track toast.id) {
        <div class="toast" [attr.data-kind]="toast.kind" role="status">
          <span>{{ toast.text }}</span>
          <button type="button" class="icon-btn" aria-label="Dismiss" (click)="toasts.dismiss(toast.id)">×</button>
        </div>
      }
    </div>
  `
})
export class ToastsComponent {
  readonly toasts = inject(ToastService);
}
