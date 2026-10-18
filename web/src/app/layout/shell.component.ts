import { Component, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { LookupService } from '../core/lookup.service';
import { ToastService } from '../core/toast.service';
import { errorMessage } from '../core/api-error';

interface NavItem { path: string; label: string; exact?: boolean; }

/** The frame around every signed-in page: the dark rail on the left (a top bar on phones) and the page on the right. */
@Component({
  selector: 'gf-shell',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="shell">
      <aside class="rail" [class.open]="menuOpen()">
        <div class="rail-top">
          <a class="brand" routerLink="/" (click)="menuOpen.set(false)">
            <span class="brand-mark" aria-hidden="true"><i></i><i></i></span>
            <span class="brand-name">GarageFlow</span>
          </a>
          <button type="button" class="menu-btn" aria-label="Menu" [attr.aria-expanded]="menuOpen()" (click)="menuOpen.set(!menuOpen())">Menu</button>
        </div>

        <nav class="rail-nav" aria-label="Main" (click)="menuOpen.set(false)">
          @if (auth.canBook()) {
            <a class="rail-cta" routerLink="/book">New booking</a>
          }
          @for (item of mainItems; track item.path) {
            <a class="rail-link" [routerLink]="item.path" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: !!item.exact }">{{ item.label }}</a>
          }
          @if (auth.isAdmin()) {
            <p class="rail-group">Admin</p>
            @for (item of adminItems; track item.path) {
              <a class="rail-link" [routerLink]="item.path" routerLinkActive="active">{{ item.label }}</a>
            }
          }
        </nav>

        <div class="rail-user">
          <div>
            <p class="rail-user-name">{{ auth.session()?.displayName }}</p>
            <p class="rail-user-role">{{ auth.role() }}</p>
          </div>
          <button type="button" class="rail-signout" (click)="signOut()">Sign out</button>
        </div>
      </aside>

      <main class="stage" id="main">
        @if (loadFailed()) {
          <div class="banner error" role="alert">
            {{ loadFailed() }} <button type="button" class="link-btn" (click)="retry()">Try again</button>
          </div>
        }
        <router-outlet />
      </main>
    </div>
  `
})
export class ShellComponent implements OnInit {
  readonly auth = inject(AuthService);
  private readonly lookups = inject(LookupService);
  private readonly toasts = inject(ToastService);
  private readonly router = inject(Router);

  readonly menuOpen = signal(false);
  readonly loadFailed = signal('');

  readonly mainItems: NavItem[] = [
    { path: '/', label: 'Bay board', exact: true },
    { path: '/appointments', label: 'Appointments' },
    { path: '/customers', label: 'Customers' },
    { path: '/waitlist', label: 'Waitlist' }
  ];
  readonly adminItems: NavItem[] = [
    { path: '/admin/shop', label: 'Shop setup' },
    { path: '/admin/users', label: 'Users' },
    { path: '/audit', label: 'Audit trail' },
    { path: '/admin/operations', label: 'Operations' }
  ];

  ngOnInit(): void {
    this.retry();
  }

  retry(): void {
    this.loadFailed.set('');
    this.lookups.load().subscribe({
      error: (error: unknown) => this.loadFailed.set(errorMessage(error))
    });
  }

  signOut(): void {
    this.lookups.reset();
    this.auth.logout();
    this.toasts.info('Signed out.');
  }
}
