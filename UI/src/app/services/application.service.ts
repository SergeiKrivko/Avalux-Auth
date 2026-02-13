import {inject, Injectable} from '@angular/core';
import {ApiClient, Application, ApplicationParameters, CreateApplicationSchema} from './api-client';
import {ApplicationEntity, ApplicationParametersEntity} from '../entities/application-entity';
import {patchState, signalState} from '@ngrx/signals';
import moment from 'moment';
import {map, NEVER, Observable, switchMap, tap} from 'rxjs';
import {AuthService} from './auth.service';
import {toObservable} from '@angular/core/rxjs-interop';

interface ApplicationStore {
  applications: ApplicationEntity[];
  selectedApplication: ApplicationEntity | null;
  isLoaded: boolean;
}

@Injectable({
  providedIn: 'root',
})
export class ApplicationService {
  private readonly apiClient = inject(ApiClient);
  private readonly authService = inject(AuthService);

  private readonly store$$ = signalState<ApplicationStore>({
    applications: [],
    selectedApplication: null,
    isLoaded: false,
  });

  readonly applications$ = toObservable(this.store$$.applications);
  readonly selectedApplication$ = toObservable(this.store$$.selectedApplication);

  loadApplicationsOnAuthChange$ = this.authService.isAuthenticated$.pipe(
    tap(() => patchState(this.store$$, {
      applications: [],
      selectedApplication: null,
      isLoaded: false,
    })),
    switchMap(isAuthenticated => {
      if (isAuthenticated)
        return this.loadApplications();
      return NEVER;
    }),
  )

  private loadApplications() {
    return this.apiClient.appsAll().pipe(
      map(apps => apps.map(applicationToEntity)),
      tap(apps => patchState(this.store$$, {
        applications: apps,
        selectedApplication: null,
        isLoaded: true,
      })),
    )
  }

  createNewApplication(name: string) {
    return this.apiClient.appsPOST(CreateApplicationSchema.fromJS({name})).pipe(
      switchMap(() => this.loadApplications()),
      map(() => true),
    );
  }

  applicationById(id: string): Observable<ApplicationEntity | undefined> {
    return this.applications$.pipe(
      map(apps => apps.find(app => app.id === id)),
    );
  }

  selectApplication(app: ApplicationEntity) {
    patchState(this.store$$, {
      selectedApplication: app,
    })
  }

  updateApplication(applicationId: string, parameters: ApplicationParametersEntity) {
    return this.apiClient.appsPUT(applicationId, ApplicationParameters.fromJS({
      name: parameters.name,
      redirectUrls: parameters.redirectUrls,
    })).pipe(
      switchMap(() => this.loadApplications())
    );
  }
}

const applicationToEntity = (app: Application): ApplicationEntity => {
  if (app.id === null)
    throw new Error("Application ID is required");
  return {
    id: app.id,
    clientId: app.clientId ?? "Error",
    clientSecret: app.clientSecret ?? "Error",
    parameters: {
      name: app.parameters.name ?? "",
      redirectUrls: app.parameters.redirectUrls ?? [],
    },
    createdAt: app.createdAt ?? moment(),
    deletedAt: app.deletedAt ?? null,
  }
}
