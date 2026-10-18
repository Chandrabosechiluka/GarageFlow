import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { toApiError } from './api-error';
import { AuthService } from './auth.service';
import { ConfigService } from './config.service';

/** Adds "Authorization: Bearer <token>" to calls to our own API (never to other websites). */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const config = inject(ConfigService);
  const token = auth.token();
  const isOurApi = request.url.startsWith(config.apiBaseUrl + '/api/');
  if (token && isOurApi) {
    return next(request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
  }
  return next(request);
};

/**
 * Turns every failed call into an ApiError with a readable message.
 * A 401 on any call except the login itself means the session ended, so we sign out.
 */
export const errorInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  return next(request).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse) {
        if (error.status === 401 && !request.url.endsWith('/api/auth/login') && auth.isSignedIn()) {
          auth.logout('expired');
        }
        return throwError(() => toApiError(error));
      }
      return throwError(() => error);
    })
  );
};
