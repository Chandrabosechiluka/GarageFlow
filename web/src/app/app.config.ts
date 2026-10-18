import { ApplicationConfig, inject, provideAppInitializer, provideZoneChangeDetection } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { routes } from './app.routes';
import { ConfigService } from './core/config.service';
import { authInterceptor, errorInterceptor } from './core/interceptors';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes, withComponentInputBinding()),
    // Order matters: first the token is added, then any failure is translated on the way back.
    provideHttpClient(withInterceptors([authInterceptor, errorInterceptor])),
    // Read /config.json before anything else runs, so every API call knows where the API lives.
    provideAppInitializer(() => inject(ConfigService).load())
  ]
};
