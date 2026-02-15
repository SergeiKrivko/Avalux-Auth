import {inject, Injectable} from '@angular/core';
import {TokenEntity, TokenPermissionEntity} from '../entities/token-entity';
import {ApiClient, CreateTokenSchema, Token, TokenPermission} from './api-client';
import {patchState, signalState} from '@ngrx/signals';
import {ApplicationService} from './application.service';
import {combineLatest, map, NEVER, Observable, switchMap, tap} from 'rxjs';
import {toObservable} from '@angular/core/rxjs-interop';
import {Moment} from 'moment';

interface TokenStore {
  tokens: TokenEntity[];
  permissions: TokenPermissionEntity[];
}

@Injectable({
  providedIn: 'root',
})
export class TokenService {
  private readonly apiClient = inject(ApiClient);
  private readonly applicationService = inject(ApplicationService);

  private readonly store$$ = signalState<TokenStore>({
    tokens: [],
    permissions: [],
  });

  readonly tokens$ = toObservable(this.store$$.tokens);
  readonly permissions$ = toObservable(this.store$$.permissions);

  readonly loadTokensOnApplicationChange$ = combineLatest([
    this.loadTokens(),
    this.loadPermissions(),
  ]).pipe(
    switchMap(() => NEVER),
  );

  private loadPermissions() {
    return this.applicationService.selectedApplication$.pipe(
      switchMap(application => {
        if (application)
          return this.apiClient.permissions(application.id)
        return NEVER;
      }),
      map(permissions => permissions.map(tokenPermissionToEntity)),
      tap(permissions => patchState(this.store$$, {permissions})),
    )
  }

  private loadTokens() {
    return this.applicationService.selectedApplication$.pipe(
      switchMap(application => {
        if (application)
          return this.apiClient.tokensAll(application.id)
        return NEVER;
      }),
      map(tokens => tokens.map(tokenToEntity)),
      tap(tokens => patchState(this.store$$, {tokens})),
    )
  }

  createNewToken(name: string, permissions: string[], expiresAt: Moment): Observable<string | undefined> {
    return this.applicationService.selectedApplication$.pipe(
      switchMap(application => {
        if (application)
          return this.apiClient.tokensPOST(application.id, CreateTokenSchema.fromJS({
            name, permissions, expiresAt
          }));
        return NEVER;
      }),
      map(resp => resp.token),
      switchMap(token => this.loadTokens().pipe(map(() => token))),
    );
  }

  deleteToken(id: string) {
    return this.applicationService.selectedApplication$.pipe(
      switchMap(application => {
        if (application)
          return this.apiClient.tokensDELETE(application.id, id);
        return NEVER;
      }),
      switchMap(token => this.loadTokens().pipe(map(() => token))),
    );
  }
}

const tokenToEntity = (token: Token): TokenEntity => {
  if (token.id === null)
    throw new Error("Application ID is required");
  return {
    id: token.id,
    name: token.name,
    permissions: token.permissions ?? [],
    createdAt: token.createdAt,
    expiresAt: token.expiresAt,
  }
}

const tokenPermissionToEntity = (token: TokenPermission): TokenPermissionEntity => {
  return {
    key: token.key ?? "",
    description: token.description ?? "",
  }
}
