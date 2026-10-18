import { Component, inject, isDevMode, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { errorMessage } from '../../core/api-error';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'gf-login',
  standalone: true,
  imports: [ReactiveFormsModule],
  template: `
    <div class="login">
      <section class="art" aria-hidden="true">
        <div class="art-board">
          <div class="art-row"><span class="art-bay">Bay 1</span><i style="left: 6%; width: 28%" class="b1"></i><i style="left: 40%; width: 18%" class="b2"></i><i style="left: 64%; width: 30%" class="b3"></i></div>
          <div class="art-row"><span class="art-bay">Bay 2</span><i style="left: 14%; width: 22%" class="b3"></i><i style="left: 44%; width: 34%" class="b1"></i></div>
          <div class="art-row"><span class="art-bay">Bay 3</span><i style="left: 2%; width: 16%" class="b2"></i><i style="left: 26%; width: 26%" class="b3"></i><i style="left: 70%; width: 24%" class="b1"></i></div>
          <span class="art-now"></span>
        </div>
        <h1>Every bay, every hour, on one board.</h1>
        <p>Book a car in, check it in and inspect it, without two cars ever landing in the same bay.</p>
      </section>

      <section class="panel">
        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <h2>Sign in</h2>

          @if (reason() === 'expired') {
            <div class="banner warn" role="status">Your session ended. Sign in again to continue.</div>
          }
          @if (error()) {
            <div class="banner error" role="alert">{{ error() }}</div>
          }

          <div class="field">
            <label for="email">Email</label>
            <input id="email" type="email" formControlName="email" autocomplete="username" [class.invalid]="showError('email')">
            @if (showError('email')) { <span class="error">Enter the email you were given.</span> }
          </div>
          <div class="field">
            <label for="password">Password</label>
            <input id="password" type="password" formControlName="password" autocomplete="current-password" [class.invalid]="showError('password')">
            @if (showError('password')) { <span class="error">Enter your password.</span> }
          </div>

          <button class="btn full" type="submit" [disabled]="busy()">{{ busy() ? 'Signing in…' : 'Sign in' }}</button>

          @if (demo) {
            <div class="demo">
              <p class="muted small">Local demo accounts (development build only)</p>
              <div class="btn-row">
                <button type="button" class="btn ghost sm" (click)="fill('admin@garageflow.local')">Admin</button>
                <button type="button" class="btn ghost sm" (click)="fill('advisor@garageflow.local')">Advisor</button>
                <button type="button" class="btn ghost sm" (click)="fill('tech@garageflow.local')">Technician</button>
              </div>
            </div>
          }
        </form>
      </section>
    </div>
  `,
  styles: [`
    .login { min-height: 100vh; display: grid; grid-template-columns: minmax(0, 1.15fr) minmax(0, 1fr); }
    .art { background: var(--rail); color: #fff; padding: 3rem clamp(1.5rem, 5vw, 4.5rem); display: flex; flex-direction: column; justify-content: center; gap: 1.1rem; }
    .art h1 { font-size: clamp(1.9rem, 3.4vw, 3rem); max-width: 16ch; }
    .art p { color: var(--rail-ink); max-width: 38ch; font-size: 1.05rem; }
    .art-board { position: relative; display: grid; gap: 0.6rem; margin-bottom: 1.5rem; padding: 1.1rem 1rem 1.1rem 4.2rem; border: 1px solid var(--rail-line); border-radius: 6px; max-width: 34rem; }
    .art-row { position: relative; height: 2.1rem; background: repeating-linear-gradient(90deg, transparent 0, transparent calc(12.5% - 1px), var(--rail-line) calc(12.5% - 1px), var(--rail-line) 12.5%); }
    .art-bay { position: absolute; left: -3.3rem; top: 50%; transform: translateY(-50%); color: var(--rail-dim); font-size: 0.82rem; }
    .art-row i { position: absolute; top: 0; bottom: 0; border-radius: 4px; border-left: 4px solid; }
    .b1 { background: #2a4c63; border-color: #6f97b6; }
    .b2 { background: #0f5a60; border-color: #3fb0b8; }
    .b3 { background: #4a3f12; border-color: var(--signal); }
    .art-now { position: absolute; top: 0.6rem; bottom: 0.6rem; left: 58%; width: 2px; background: var(--signal); }
    .panel { display: grid; place-items: center; padding: 2rem 1.5rem; background: var(--concrete); }
    form { width: min(24rem, 100%); }
    form h2 { font-size: 1.6rem; margin-bottom: 1.25rem; }
    .btn.full { width: 100%; margin-top: 0.25rem; }
    .demo { margin-top: 1.75rem; padding-top: 1rem; border-top: 1px solid var(--line); display: grid; gap: 0.5rem; }
    @media (max-width: 860px) {
      .login { grid-template-columns: 1fr; }
      .art { padding: 1.75rem 1.25rem; }
      .art-board { display: none; }
    }
  `]
})
export class LoginComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly demo = isDevMode();
  readonly busy = signal(false);
  readonly error = signal('');
  readonly reason = signal<string>(this.route.snapshot.queryParamMap.get('reason') ?? '');

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]]
  });

  showError(name: 'email' | 'password'): boolean {
    const control = this.form.controls[name];
    return control.invalid && (control.touched || control.dirty);
  }

  fill(email: string): void {
    this.form.setValue({ email, password: 'Garage@12345' });
  }

  submit(): void {
    this.error.set('');
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.busy.set(true);
    const { email, password } = this.form.getRawValue();
    this.auth.login(email.trim(), password).subscribe({
      next: () => {
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
        // Only follow addresses inside this app (starting with a single "/"), never an address someone put in the link.
        const safe = returnUrl && returnUrl.startsWith('/') && !returnUrl.startsWith('//') ? returnUrl : '/';
        void this.router.navigateByUrl(safe);
      },
      error: (error: unknown) => {
        this.busy.set(false);
        this.error.set(errorMessage(error));
      }
    });
  }
}
