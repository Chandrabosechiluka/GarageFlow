import { Component, Input } from '@angular/core';

@Component({
  selector: 'gf-page-header',
  standalone: true,
  template: `
    <header class="page-head">
      <div>
        <h1>{{ title }}</h1>
        @if (subtitle) { <p class="muted">{{ subtitle }}</p> }
      </div>
      <div class="page-actions"><ng-content></ng-content></div>
    </header>
  `
})
export class PageHeaderComponent {
  @Input() title = '';
  @Input() subtitle = '';
}
