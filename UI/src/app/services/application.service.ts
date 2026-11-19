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
      requireClientSecret: parameters.requireClientSecret,
    })).pipe(
      switchMap(() => this.loadApplications())
    );
  }

  /**
   * Uploads (or replaces) the application icon. The API normalises it to a
   * 256px PNG and stores it in S3, so the panel only has to send the file.
   * The returned URL is patched into the store: a full reload would reset the
   * configuration form and drop unsaved edits.
   */
  uploadIcon(applicationId: string, file: File) {
    return this.apiClient.appIconPOST(applicationId, {data: file, fileName: file.name}).pipe(
      tap(iconUrl => this.patchIcon(applicationId, iconUrl ?? null)),
    );
  }

  deleteIcon(applicationId: string) {
    return this.apiClient.appIconDELETE(applicationId).pipe(
      tap(() => this.patchIcon(applicationId, null)),
    );
  }

  private patchIcon(applicationId: string, iconUrl: string | null) {
    const selected = this.store$$.selectedApplication();
    patchState(this.store$$, {
      applications: this.store$$.applications()
        .map(app => app.id === applicationId ? {...app, iconUrl} : app),
      selectedApplication: selected?.id === applicationId ? {...selected, iconUrl} : selected,
    });
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
      requireClientSecret: app.parameters.requireClientSecret ?? true,
    },
    iconUrl: app.iconUrl ?? null,
    createdAt: app.createdAt ?? moment(),
    deletedAt: app.deletedAt ?? null,
  }
}
