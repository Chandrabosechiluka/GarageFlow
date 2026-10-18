import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ApiError } from './api-error';
import { AuthService } from './auth.service';
import { ConfigService } from './config.service';
import { authInterceptor, errorInterceptor } from './interceptors';

describe('interceptors', () => {
  let http: HttpClient;
  let controller: HttpTestingController;
  const auth = { token: () => 'tok', isSignedIn: () => true, logout: jasmine.createSpy('logout') };

  beforeEach(() => {
    auth.logout.calls.reset();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([authInterceptor, errorInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: auth },
        { provide: ConfigService, useValue: { apiBaseUrl: 'http://api.test' } }
      ]
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => controller.verify());

  it('adds the bearer token to our API only', () => {
    http.get('http://api.test/api/customers').subscribe();
    expect(controller.expectOne('http://api.test/api/customers').request.headers.get('Authorization')).toBe('Bearer tok');

    http.get('https://other.example/data').subscribe();
    expect(controller.expectOne('https://other.example/data').request.headers.has('Authorization')).toBeFalse();
  });

  it('signs out on a 401 from a normal call and maps the error', () => {
    let caught: unknown;
    http.get('http://api.test/api/customers').subscribe({ error: (e) => (caught = e) });
    controller.expectOne('http://api.test/api/customers').flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(auth.logout).toHaveBeenCalledWith('expired');
    expect(caught instanceof ApiError).toBeTrue();
  });

  it('does not sign out when the login itself fails', () => {
    http.post('http://api.test/api/auth/login', {}).subscribe({ error: () => undefined });
    controller.expectOne('http://api.test/api/auth/login').flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(auth.logout).not.toHaveBeenCalled();
  });
});
