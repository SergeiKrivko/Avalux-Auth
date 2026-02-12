import {ApplicationConfig, provideZoneChangeDetection} from '@angular/core';
import { provideRouter } from '@angular/router';

import { routes } from './app.routes';
import {API_BASE_URL} from './services/api-client';
import {provideHttpClient} from '@angular/common/http';
import {provideAnimations} from '@angular/platform-browser/animations';
import {NG_EVENT_PLUGINS} from '@taiga-ui/event-plugins';

export const appConfig: ApplicationConfig = {
  providers: [
    { provide: API_BASE_URL, useValue: "." },
    // { provide: API_BASE_URL, useValue: "http://localhost:5000" },
    // { provide: API_BASE_URL, useValue: "https://auth.nachert.art" },
    provideAnimations(),
    provideZoneChangeDetection({eventCoalescing: true}),
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideHttpClient(),
    NG_EVENT_PLUGINS
  ]
};
