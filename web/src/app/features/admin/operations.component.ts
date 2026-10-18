import { Component, OnInit, inject, signal } from '@angular/core';
import { errorMessage } from '../../core/api-error';
import { ApiService } from '../../core/api.service';
import { MaintenanceReport } from '../../core/models';
import { PageHeaderComponent } from '../../shared/page-header.component';

@Component({
  selector: 'gf-operations',
  standalone: true,
  imports: [PageHeaderComponent],
  template: `
    <gf-page-header title="Operations" subtitle="The background jobs run on their own every few minutes. Run them now if you don't want to wait." />

    <section class="section">
      <h2>Service health</h2>
      <dl class="facts">
        <dt>API</dt><dd>{{ api$() }}</dd>
        <dt>Database</dt><dd>{{ db$() }}</dd>
      </dl>
      <button type="button" class="btn ghost sm" (click)="checkHealth()">Check again</button>
    </section>

    <section class="section">
      <h2>Run maintenance</h2>
      <p class="muted">Marks missed appointments as no-shows, sends reminders, notifies the next person on the waitlist and expires old offers.</p>
      @if (error()) { <div class="banner error" role="alert">{{ error() }}</div> }
      <button type="button" class="btn" (click)="run()" [disabled]="running()">{{ running() ? 'Running…' : 'Run now' }}</button>
      @if (report(); as r) {
        <dl class="facts report" aria-live="polite">
          <dt>No-shows marked</dt><dd>{{ r.noShowsMarked }}</dd>
          <dt>Reminders sent</dt><dd>{{ r.remindersSent }}</dd>
          <dt>Offers sent</dt><dd>{{ r.offersNotified }}</dd>
          <dt>Offers expired</dt><dd>{{ r.offersExpired }}</dd>
        </dl>
        @if (r.total === 0) { <p class="muted">Nothing needed doing.</p> }
      }
    </section>
  `,
  styles: [`.report { margin-top: 1.25rem; }`]
})
export class OperationsComponent implements OnInit {
  private readonly api = inject(ApiService);

  readonly api$ = signal('Checking…');
  readonly db$ = signal('Checking…');
  readonly running = signal(false);
  readonly error = signal('');
  readonly report = signal<MaintenanceReport | null>(null);

  ngOnInit(): void { this.checkHealth(); }

  checkHealth(): void {
    this.api$.set('Checking…');
    this.db$.set('Checking…');
    this.api.health().subscribe({ next: (h) => this.api$.set(h.status), error: () => this.api$.set('Not reachable') });
    this.api.healthDb().subscribe({ next: (h) => this.db$.set(h.database), error: () => this.db$.set('Not reachable') });
  }

  run(): void {
    this.running.set(true);
    this.error.set('');
    this.api.runMaintenance().subscribe({
      next: (r) => { this.report.set(r); this.running.set(false); },
      error: (error: unknown) => { this.running.set(false); this.error.set(errorMessage(error)); }
    });
  }
}
