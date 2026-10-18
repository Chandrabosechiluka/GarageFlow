import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { ApiService } from './api.service';
import { LoginResponse, UserRole } from './models';

export interface Session {
  token: string;
  role: UserRole;
  displayName: string;
  email: string;
  expiresAtUtc: string;
}

const STORAGE_KEY = 'gf.session';

/** Reads one claim from a JWT. The token is only decoded for display; the API is what actually trusts it. */
export function readClaim(token: string, claim: string): string {
  try {
    const payload = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    const json = decodeURIComponent(
      atob(payload).split('').map((c) => '%' + c.charCodeAt(0).toString(16).padStart(2, '0')).join('')
    );
    const value = (JSON.parse(json) as Record<string, unknown>)[claim];
    return typeof value === 'string' ? value : '';
  } catch {
    return '';
  }
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);

  private readonly state = signal<Session | null>(this.restore());

  readonly session = this.state.asReadonly();
  readonly isSignedIn = computed(() => this.state() !== null);
  readonly role = computed<UserRole | null>(() => this.state()?.role ?? null);
  readonly email = computed(() => this.state()?.email ?? '');
  readonly isAdmin = computed(() => this.state()?.role === 'Admin');
  /** Admins and advisors work with customers and bookings. */
  readonly canBook = computed(() => this.state()?.role === 'Admin' || this.state()?.role === 'Advisor');
  /** Admins and technicians do the inspection work. */
  readonly canInspect = computed(() => this.state()?.role === 'Admin' || this.state()?.role === 'Technician');

  login(email: string, password: string): Observable<LoginResponse> {
    return this.api.login(email, password).pipe(
      tap((response) => {
        const session: Session = {
          token: response.accessToken,
          role: response.role,
          displayName: response.displayName,
          email: readClaim(response.accessToken, 'email') || email,
          expiresAtUtc: response.expiresAtUtc
        };
        this.state.set(session);
        // sessionStorage: survives a page reload, but is gone when the tab closes. Never localStorage for a token.
        try { sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session)); } catch { /* storage unavailable: stay signed in for this page only */ }
      })
    );
  }

  logout(reason?: 'expired'): void {
    this.state.set(null);
    try { sessionStorage.removeItem(STORAGE_KEY); } catch { /* ignore */ }
    void this.router.navigate(['/login'], reason ? { queryParams: { reason } } : {});
  }

  token(): string | null {
    return this.state()?.token ?? null;
  }

  /** True while the token has not run out. */
  hasValidToken(now: Date = new Date()): boolean {
    const session = this.state();
    return session !== null && new Date(session.expiresAtUtc).getTime() > now.getTime();
  }

  hasRole(...roles: UserRole[]): boolean {
    const role = this.state()?.role;
    return role !== undefined && roles.includes(role);
  }

  private restore(): Session | null {
    try {
      const raw = sessionStorage.getItem(STORAGE_KEY);
      if (!raw) return null;
      const session = JSON.parse(raw) as Session;
      return new Date(session.expiresAtUtc).getTime() > Date.now() ? session : null;
    } catch {
      return null;
    }
  }
}
