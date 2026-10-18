import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ToastsComponent } from './shared/toasts.component';

@Component({
  selector: 'gf-root',
  standalone: true,
  imports: [RouterOutlet, ToastsComponent],
  template: `<router-outlet /><gf-toasts />`
})
export class AppComponent {}
