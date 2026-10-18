import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { errorMessage } from '../../core/api-error';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { Customer, PagedResult } from '../../core/models';
import { NewCustomerModalComponent } from '../../shared/customer-modals.component';
import { EmptyComponent } from '../../shared/empty.component';
import { PageHeaderComponent } from '../../shared/page-header.component';
import { PagerComponent } from '../../shared/pager.component';

@Component({
  selector: 'gf-customer-list',
  standalone: true,
  imports: [RouterLink, EmptyComponent, NewCustomerModalComponent, PageHeaderComponent, PagerComponent],
  template: `
    <gf-page-header title="Customers" subtitle="Search by name, email or phone.">
      @if (auth.canBook()) { <button type="button" class="btn" (click)="creating.set(true)">New customer</button> }
    </gf-page-header>

    <section class="section plain">
      <div class="filters">
        <div class="field grow">
          <label for="q">Search</label>
          <input id="q" type="search" placeholder="Priya, priya@example.com, 9000…" [value]="query()" (input)="onSearch($any($event.target).value)">
        </div>
      </div>

      @if (error()) { <div class="banner error" role="alert">{{ error() }}</div> }
      @if (loading()) { <p class="loading">Loading customers…</p> }

      @if (!loading() && result(); as r) {
        @if (r.items.length === 0) {
          <gf-empty [title]="query() ? 'No customer matches that search' : 'No customers yet'" [text]="query() ? 'Check the spelling, or add them as a new customer.' : 'Add the first customer to start booking cars in.'">
            @if (auth.canBook()) { <button type="button" class="btn" (click)="creating.set(true)">New customer</button> }
          </gf-empty>
        } @else {
          <div class="table-wrap">
            <table class="data">
              <thead><tr><th>Name</th><th>Email</th><th>Phone</th><th>Cars</th></tr></thead>
              <tbody>
                @for (c of r.items; track c.id) {
                  <tr>
                    <td><a class="row-link" [routerLink]="['/customers', c.id]">{{ c.fullName }}</a></td>
                    <td>{{ c.email }}</td>
                    <td class="nowrap">{{ c.phone }}</td>
                    <td>{{ c.vehicles.length }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
        <gf-pager [page]="r.page" [pageSize]="r.pageSize" [totalPages]="r.totalPages" [totalCount]="r.totalCount" (pageChange)="goTo($event)" />
      }
    </section>

    @if (creating()) {
      <gf-new-customer-modal (created)="onCreated($event)" (closed)="creating.set(false)" />
    }
  `,
  styles: [`.grow { flex: 1; min-width: 14rem; max-width: 30rem; }`]
})
export class CustomerListComponent implements OnInit, OnDestroy {
  readonly auth = inject(AuthService);
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);

  readonly query = signal('');
  readonly page = signal(1);
  readonly result = signal<PagedResult<Customer> | null>(null);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly creating = signal(false);
  private timer: ReturnType<typeof setTimeout> | undefined;

  ngOnInit(): void { this.load(); }
  ngOnDestroy(): void { clearTimeout(this.timer); }

  onSearch(value: string): void {
    this.query.set(value);
    this.page.set(1);
    clearTimeout(this.timer);
    this.timer = setTimeout(() => this.load(), 250);
  }

  goTo(page: number): void {
    this.page.set(page);
    this.load();
  }

  onCreated(customer: Customer): void {
    this.creating.set(false);
    void this.router.navigate(['/customers', customer.id]);
  }

  private load(): void {
    this.loading.set(true);
    this.error.set('');
    this.api.customers(this.query().trim(), this.page(), 15).subscribe({
      next: (result) => {
        this.result.set(result);
        this.loading.set(false);
      },
      error: (error: unknown) => {
        this.loading.set(false);
        this.error.set(errorMessage(error));
      }
    });
  }
}
