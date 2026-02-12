import {inject, Injectable} from '@angular/core';
import {AdminCredentialsSchema, ApiClient} from './api-client';
import {patchState, signalState} from '@ngrx/signals';
import {toObservable} from '@angular/core/rxjs-interop';
import {catchError, map, NEVER, Observable, of, switchMap, tap} from 'rxjs';

interface AuthState {
  isLoaded: boolean;
  isAuthenticated: boolean;
}

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  private readonly apiClient = inject(ApiClient);

  private readonly store$$ = signalState<AuthState>({
    isLoaded: false,
    isAuthenticated: false,
  });

  readonly state$ = toObservable(this.store$$);
  readonly isLoaded$ = toObservable(this.store$$.isLoaded);
  readonly isAuthenticated$ = toObservable(this.store$$.isAuthenticated);

  load(): Observable<boolean> {
    return this.apiClient.test().pipe(
      map(() => {
        patchState(this.store$$, {
          isLoaded: true,
          isAuthenticated: true,
        });
        return true;
      }),
      catchError(() => {
        patchState(this.store$$, {
          isLoaded: true,
          isAuthenticated: false,
        });
        return of(false);
      })
    );
  }

  logIn(login: string, password: string): Observable<void> {
    return this.apiClient.login(AdminCredentialsSchema.fromJS({login, password})).pipe(
      tap(() => patchState(this.store$$, {
        isLoaded: true,
        isAuthenticated: true,
      })),
      catchError(() => {
        patchState(this.store$$, {
          isLoaded: true,
          isAuthenticated: false,
        });
        return NEVER;
      }),
    );
  }

  logOut(): Observable<never> {
    return this.apiClient.logout().pipe(
      tap(() => patchState(this.store$$, {
        isAuthenticated: false,
      })),
      switchMap(() => NEVER),
    );
  }
}
