import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'gf-not-found',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="center-page">
      <h1>This page doesn't exist</h1>
      <p class="muted">The address may be mistyped, or the page was moved.</p>
      <a class="btn" routerLink="/">Go to the bay board</a>
    </div>
  `
})
export class NotFoundComponent {}
