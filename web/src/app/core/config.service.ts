import { Injectable } from '@angular/core';

/**
 * Settings that can change per environment WITHOUT rebuilding the app: they are read from /config.json when the app starts.
 * apiBaseUrl = "" means "same address as the website" (the dev proxy and the nginx container both forward /api to the API).
 * Put a full address here (for example https://garageflow-api.example.com) when the API lives somewhere else;
 * the API must then list this website in Cors__AllowedOrigins.
 */
@Injectable({ providedIn: 'root' })
export class ConfigService {
  apiBaseUrl = '';

  async load(): Promise<void> {
    try {
      const response = await fetch('config.json', { cache: 'no-store' });
      if (response.ok) {
        const body = (await response.json()) as { apiBaseUrl?: string };
        this.apiBaseUrl = (body.apiBaseUrl ?? '').replace(/\/+$/, '');
      }
    } catch {
      // No config file: keep the defaults.
    }
  }
}
