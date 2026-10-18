import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { errorMessage } from '../../core/api-error';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { ALL_ROLES, UserAccount } from '../../core/models';
import { ToastService } from '../../core/toast.service';
import { BadgeComponent } from '../../shared/badge.component';
import { EmptyComponent } from '../../shared/empty.component';
import { ModalComponent } from '../../shared/modal.component';
import { PageHeaderComponent } from '../../shared/page-header.component';

@Component({
  selector: 'gf-users',
  standalone: true,
  imports: [ReactiveFormsModule, BadgeComponent, EmptyComponent, ModalComponent, PageHeaderComponent],
  template: `
    <gf-page-header title="Users" subtitle="People who can sign in. Advisors book and manage appointments; technicians run inspections.">
      <button type="button" class="btn" (click)="open()">Add a user</button>
    </gf-page-header>

    <section class="section plain">
      @if (error()) { <div class="banner error" role="alert">{{ error() }}</div> }
      @if (loading()) { <p class="loading">Loading users…</p> }
      @if (!loading() && !error()) {
        @if (users().length === 0) {
          <gf-empty title="No users" text="Add the first user." />
        } @else {
          <div class="table-wrap"><table class="data">
            <thead><tr><th>Name</th><th>Email</th><th>Role</th><th>Status</th><th></th></tr></thead>
            <tbody>@for (u of users(); track u.id) {
              <tr>
                <td><strong>{{ u.displayName }}</strong></td>
                <td>{{ u.email }}</td>
                <td>{{ u.role }}</td>
                <td><gf-badge [tone]="u.isActive ? 'done' : 'off'" [label]="u.isActive ? 'Active' : 'Deactivated'" /></td>
                <td class="right">
                  @if (u.isActive && u.email.toLowerCase() !== auth.email().toLowerCase()) {
                    <button type="button" class="btn danger-ghost sm" [disabled]="busyId() === u.id" (click)="deactivate(u)">Deactivate</button>
                  }
                </td>
              </tr>
            }</tbody>
          </table></div>
        }
      }
    </section>

    @if (creating()) {
      <gf-modal heading="Add a user" (closed)="creating.set(false)">
        <form [formGroup]="form" (ngSubmit)="create()" novalidate>
          @if (formError()) { <div class="banner error" role="alert">{{ formError() }}</div> }
          <div class="field"><label for="un">Name</label><input id="un" formControlName="displayName" autocomplete="off"></div>
          <div class="field"><label for="ue">Email</label><input id="ue" type="email" formControlName="email" autocomplete="off"></div>
          <div class="form-row">
            <div class="field"><label for="up">Password</label><input id="up" type="password" formControlName="password" autocomplete="new-password"><span class="hint">At least 10 characters.</span></div>
            <div class="field"><label for="ur">Role</label><select id="ur" formControlName="role">@for (r of roles; track r) { <option [value]="r">{{ r }}</option> }</select></div>
          </div>
          <div class="btn-row end">
            <button type="button" class="btn ghost" (click)="creating.set(false)">Cancel</button>
            <button type="submit" class="btn" [disabled]="saving()">{{ saving() ? 'Saving…' : 'Add user' }}</button>
          </div>
        </form>
      </gf-modal>
    }
  `
})
export class UsersComponent implements OnInit {
  readonly auth = inject(AuthService);
  private readonly api = inject(ApiService);
  private readonly toasts = inject(ToastService);
  private readonly fb = inject(FormBuilder);

  readonly roles = ALL_ROLES;
  readonly users = signal<UserAccount[]>([]);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly creating = signal(false);
  readonly saving = signal(false);
  readonly formError = signal('');
  readonly busyId = signal('');

  readonly form = this.fb.nonNullable.group({
    displayName: ['', [Validators.required]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(10)]],
    role: ['Advisor' as UserAccount['role']]
  });

  ngOnInit(): void { this.load(); }

  open(): void {
    this.form.reset({ displayName: '', email: '', password: '', role: 'Advisor' });
    this.formError.set('');
    this.creating.set(true);
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.api.listUsers().subscribe({
      next: (list) => { this.users.set(list); this.loading.set(false); },
      error: (error: unknown) => { this.loading.set(false); this.error.set(errorMessage(error)); }
    });
  }

  create(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.formError.set('Fill in every field. The email must be valid and the password at least 10 characters.');
      return;
    }
    this.saving.set(true);
    this.formError.set('');
    const v = this.form.getRawValue();
    this.api.createUser({ ...v, displayName: v.displayName.trim(), email: v.email.trim() }).subscribe({
      next: () => {
        this.saving.set(false);
        this.creating.set(false);
        this.toasts.success('User added.');
        this.load();
      },
      error: (error: unknown) => { this.saving.set(false); this.formError.set(errorMessage(error)); }
    });
  }

  deactivate(user: UserAccount): void {
    this.busyId.set(user.id);
    this.api.deactivateUser(user.id).subscribe({
      next: () => { this.busyId.set(''); this.toasts.success(user.displayName + ' can no longer sign in.'); this.load(); },
      error: (error: unknown) => { this.busyId.set(''); this.toasts.error(errorMessage(error)); }
    });
  }
}
