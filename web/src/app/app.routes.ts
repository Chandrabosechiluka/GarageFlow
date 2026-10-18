import { Routes } from '@angular/router';
import { authGuard, roleGuard } from './core/guards';
import { ShellComponent } from './layout/shell.component';

// Every page is loaded on demand (loadComponent), so the first download stays small.
export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./features/login/login.component').then((m) => m.LoginComponent), title: 'Sign in · GarageFlow' },
  {
    path: '',
    component: ShellComponent,
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', loadComponent: () => import('./features/board/board.component').then((m) => m.BoardComponent), title: 'Bay board · GarageFlow' },
      { path: 'appointments', loadComponent: () => import('./features/appointments/appointment-list.component').then((m) => m.AppointmentListComponent), title: 'Appointments · GarageFlow' },
      { path: 'appointments/:id', loadComponent: () => import('./features/appointments/appointment-detail.component').then((m) => m.AppointmentDetailComponent), title: 'Appointment · GarageFlow' },
      { path: 'book', canActivate: [roleGuard('Admin', 'Advisor')], loadComponent: () => import('./features/booking/booking.component').then((m) => m.BookingComponent), title: 'New booking · GarageFlow' },
      { path: 'customers', loadComponent: () => import('./features/customers/customer-list.component').then((m) => m.CustomerListComponent), title: 'Customers · GarageFlow' },
      { path: 'customers/:id', loadComponent: () => import('./features/customers/customer-detail.component').then((m) => m.CustomerDetailComponent), title: 'Customer · GarageFlow' },
      { path: 'waitlist', loadComponent: () => import('./features/waitlist/waitlist.component').then((m) => m.WaitlistComponent), title: 'Waitlist · GarageFlow' },
      { path: 'admin/shop', canActivate: [roleGuard('Admin')], loadComponent: () => import('./features/admin/shop-setup.component').then((m) => m.ShopSetupComponent), title: 'Shop setup · GarageFlow' },
      { path: 'admin/users', canActivate: [roleGuard('Admin')], loadComponent: () => import('./features/admin/users.component').then((m) => m.UsersComponent), title: 'Users · GarageFlow' },
      { path: 'admin/operations', canActivate: [roleGuard('Admin')], loadComponent: () => import('./features/admin/operations.component').then((m) => m.OperationsComponent), title: 'Operations · GarageFlow' },
      { path: 'audit', canActivate: [roleGuard('Admin')], loadComponent: () => import('./features/audit/audit.component').then((m) => m.AuditComponent), title: 'Audit trail · GarageFlow' }
    ]
  },
  { path: '**', loadComponent: () => import('./features/not-found.component').then((m) => m.NotFoundComponent), title: 'Not found · GarageFlow' }
];
