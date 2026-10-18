import { Component, inject, signal } from '@angular/core';
import { Observable } from 'rxjs';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { errorMessage } from '../../core/api-error';
import { ApiService } from '../../core/api.service';
import { LookupService } from '../../core/lookup.service';
import { ALL_SKILLS, Skill } from '../../core/models';
import { skillLabel } from '../../core/status';
import { ToastService } from '../../core/toast.service';
import { EmptyComponent } from '../../shared/empty.component';
import { ModalComponent } from '../../shared/modal.component';
import { PageHeaderComponent } from '../../shared/page-header.component';

type Kind = 'service' | 'technician' | 'bay' | null;

@Component({
  selector: 'gf-shop-setup',
  standalone: true,
  imports: [ReactiveFormsModule, EmptyComponent, ModalComponent, PageHeaderComponent],
  template: `
    <gf-page-header title="Shop setup" subtitle="What the shop offers, who does the work and where. Changes show up in booking straight away." />

    <section class="section">
      <div class="section-head"><h2>Services</h2><button type="button" class="btn ghost sm" (click)="open('service')">Add a service</button></div>
      @if (lookups.serviceTypes().length === 0) {
        <gf-empty title="No services yet" text="Customers can't be booked in until the shop has at least one service." />
      } @else {
        <div class="table-wrap"><table class="data">
          <thead><tr><th>Service</th><th>Takes</th><th>Skill needed</th></tr></thead>
          <tbody>@for (s of lookups.serviceTypes(); track s.id) {
            <tr><td><strong>{{ s.name }}</strong></td><td>{{ s.durationMinutes }} min</td><td>{{ label(s.requiredSkill) }}</td></tr>
          }</tbody>
        </table></div>
      }
    </section>

    <section class="section">
      <div class="section-head"><h2>Technicians</h2><button type="button" class="btn ghost sm" (click)="open('technician')">Add a technician</button></div>
      @if (lookups.technicians().length === 0) {
        <gf-empty title="No technicians yet" text="A service needs a technician with the matching skill." />
      } @else {
        <div class="table-wrap"><table class="data">
          <thead><tr><th>Name</th><th>Skills</th></tr></thead>
          <tbody>@for (t of lookups.technicians(); track t.id) {
            <tr><td><strong>{{ t.name }}</strong></td><td>{{ skillsText(t.skills) }}</td></tr>
          }</tbody>
        </table></div>
      }
    </section>

    <section class="section">
      <div class="section-head"><h2>Bays</h2><button type="button" class="btn ghost sm" (click)="open('bay')">Add a bay</button></div>
      @if (lookups.bays().length === 0) {
        <gf-empty title="No bays yet" text="Each car on the board needs a bay." />
      } @else {
        <ul class="plain-list">@for (b of lookups.bays(); track b.id) { <li>{{ b.name }}</li> }</ul>
      }
    </section>

    @if (kind(); as k) {
      <gf-modal [heading]="k === 'service' ? 'Add a service' : k === 'technician' ? 'Add a technician' : 'Add a bay'" (closed)="close()">
        <form (ngSubmit)="save()" [formGroup]="form" novalidate>
          @if (error()) { <div class="banner error" role="alert">{{ error() }}</div> }
          <div class="field">
            <label for="nm">Name</label>
            <input id="nm" formControlName="name" maxlength="100" autocomplete="off">
          </div>
          @if (k === 'service') {
            <div class="form-row">
              <div class="field">
                <label for="du">Minutes</label>
                <input id="du" type="number" min="15" max="480" step="15" formControlName="durationMinutes">
              </div>
              <div class="field">
                <label for="sk">Skill needed</label>
                <select id="sk" formControlName="skill">@for (s of skills; track s) { <option [value]="s">{{ label(s) }}</option> }</select>
              </div>
            </div>
          }
          @if (k === 'technician') {
            <fieldset class="field">
              <legend>Skills</legend>
              <div class="chips">
                @for (s of skills; track s) {
                  <label class="chip-check"><input type="checkbox" [checked]="picked().includes(s)" (change)="toggle(s)"> {{ label(s) }}</label>
                }
              </div>
            </fieldset>
          }
          <div class="btn-row end">
            <button type="button" class="btn ghost" (click)="close()">Cancel</button>
            <button type="submit" class="btn" [disabled]="saving()">{{ saving() ? 'Saving…' : 'Save' }}</button>
          </div>
        </form>
      </gf-modal>
    }
  `,
  styles: [`.plain-list { list-style: none; margin: 0; padding: 0; display: flex; flex-wrap: wrap; gap: 0.5rem; } .plain-list li { padding: 0.4rem 0.8rem; background: var(--sunk); border-radius: 4px; font-weight: 600; } fieldset.field { border: 0; padding: 0; margin: 0 0 1rem; } legend { font-weight: 600; margin-bottom: 0.4rem; padding: 0; }`]
})
export class ShopSetupComponent {
  readonly lookups = inject(LookupService);
  private readonly api = inject(ApiService);
  private readonly toasts = inject(ToastService);
  private readonly fb = inject(FormBuilder);

  readonly skills = ALL_SKILLS;
  readonly kind = signal<Kind>(null);
  readonly picked = signal<Skill[]>([]);
  readonly saving = signal(false);
  readonly error = signal('');

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
    durationMinutes: [60, [Validators.required, Validators.min(15), Validators.max(480)]],
    skill: ['General' as Skill]
  });

  label(s: string): string { return skillLabel(s); }
  skillsText(list: Skill[]): string { return list.length ? list.map((s) => skillLabel(s)).join(', ') : 'None'; }

  open(kind: Kind): void {
    this.form.reset({ name: '', durationMinutes: 60, skill: 'General' });
    this.picked.set([]);
    this.error.set('');
    this.kind.set(kind);
  }

  close(): void { this.kind.set(null); }

  toggle(skill: Skill): void {
    const current = this.picked();
    this.picked.set(current.includes(skill) ? current.filter((s) => s !== skill) : [...current, skill]);
  }

  save(): void {
    const kind = this.kind();
    const v = this.form.getRawValue();
    const name = v.name.trim();
    if (!kind) return;
    if (!name) { this.error.set('Give it a name.'); return; }
    if (kind === 'service' && (v.durationMinutes < 15 || v.durationMinutes > 480)) { this.error.set('A service takes between 15 and 480 minutes.'); return; }
    if (kind === 'technician' && this.picked().length === 0) { this.error.set('Pick at least one skill.'); return; }

    this.saving.set(true);
    this.error.set('');
    const request: Observable<unknown> =
      kind === 'service' ? this.api.createServiceType({ name, durationMinutes: Number(v.durationMinutes), requiredSkill: v.skill })
      : kind === 'technician' ? this.api.createTechnician({ name, skills: this.picked() })
      : this.api.createBay({ name });

    request.subscribe({
      next: () => {
        this.saving.set(false);
        this.kind.set(null);
        this.toasts.success('Saved.');
        this.lookups.load(true).subscribe();
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.error.set(errorMessage(error));
      }
    });
  }
}
