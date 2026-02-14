import {inject, Injectable} from '@angular/core';
import {ProviderEntity, ProviderInfoEntity, ProviderParametersEntity} from '../entities/provider-entity';
import {
  ApiClient,
  CreateProviderSchema,
  Provider,
  ProviderInfo,
  ProviderParameters
} from './api-client';
import {patchState, signalState} from '@ngrx/signals';
import {toObservable} from '@angular/core/rxjs-interop';
import {first, from, map, NEVER, Observable, switchMap, tap} from 'rxjs';
import moment from 'moment/moment';
import {ApplicationService} from './application.service';

interface ProviderStore {
  addedProviders: ProviderEntity[];
  providerInfos: ProviderInfoEntity[];
  selectedProvider: ProviderEntity | null;
  isLoaded: boolean;
}

@Injectable({
  providedIn: 'root',
})
export class ProviderService {
  private readonly apiClient = inject(ApiClient);
  private readonly applicationService = inject(ApplicationService);

  private readonly store$$ = signalState<ProviderStore>({
    addedProviders: [],
    providerInfos: [],
    selectedProvider: null,
    isLoaded: false,
  });

  readonly addedProviders$ = toObservable(this.store$$.addedProviders);
  readonly providerInfos$ = toObservable(this.store$$.providerInfos);
  readonly selectedProvider$ = toObservable(this.store$$.selectedProvider);

  loadProvidersOnApplicationChange$ = this.applicationService.selectedApplication$.pipe(
    tap(() => patchState(this.store$$, {
      addedProviders: [],
      selectedProvider: null,
      isLoaded: false,
    })),
    switchMap(app => {
      if (app)
        return this.loadProviders(app.id);
      return NEVER;
    }),
    switchMap(() => this.apiClient.providersInfo()),
    tap(infos => patchState(this.store$$, {providerInfos: infos.map(providerInfoToEntity)})),
  )

  private loadProviders(appId: string) {
    return this.apiClient.providersAll(appId).pipe(
      map(providers => providers.map(providerToEntity)),
      tap(providers => patchState(this.store$$, {
        addedProviders: providers,
        selectedProvider: null,
        isLoaded: true,
      })),
    )
  }

  createNewProvider(id: number) {
    return this.applicationService.selectedApplication$.pipe(
      first(),
      switchMap(app => {
        if (app)
          return this.apiClient.providersPOST(app.id, CreateProviderSchema.fromJS({providerId: id})).pipe(
            switchMap(id => this.loadProviders(app.id).pipe(
              map(() => id)
            )),
          );
        return NEVER;
      }),
    );
  }

  providerById(id: string): Observable<ProviderEntity | undefined> {
    return this.addedProviders$.pipe(
      map(providers => providers.find(e => e.id === id)),
    );
  }

  providerInfoById(id: number): Observable<ProviderInfoEntity | undefined> {
    return this.providerInfos$.pipe(
      map(providers => providers.find(e => e.id === id)),
    );
  }

  selectProvider(provider: ProviderEntity) {
    patchState(this.store$$, {
      selectedProvider: provider,
    })
  }

  updateProvider(id: string, parameters: ProviderParametersEntity) {
    return this.applicationService.selectedApplication$.pipe(
      first(),
      switchMap(app => {
        if (app)
          return this.apiClient.providersPUT(app.id, id, ProviderParameters.fromJS({
            clientId: parameters.clientId,
            clientSecret: parameters.clientSecret,
            saveTokens: parameters.saveTokens,
            defaultScope: parameters.defaultScope,
          }));
        return NEVER;
      }),
    );
  }
}

const providerToEntity = (provider: Provider): ProviderEntity => {
  if (provider.id === undefined || provider.providerId === undefined || provider.applicationId == undefined)
    throw new Error("Provider ID is required");
  return {
    id: provider.id,
    providerId: provider.providerId,
    applicationId: provider.applicationId,
    parameters: {
      clientId: provider.parameters.clientId,
      clientSecret: provider.parameters.clientSecret,
      saveTokens: provider.parameters.saveTokens ?? false,
      defaultScope: provider.parameters.defaultScope ?? [],
    },
    createdAt: provider.createdAt ?? moment(),
    deletedAt: provider.deletedAt ?? null,
  }
}

const providerInfoToEntity = (provider: ProviderInfo): ProviderInfoEntity => {
  if (provider.id === undefined)
    throw new Error("Provider ID is required");
  return {
    id: provider.id,
    key: provider.key ?? "",
    name: provider.name ?? "",
    url: provider.url,
  }
}
