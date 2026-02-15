import {inject, Injectable} from '@angular/core';
import {ApiClient, UserWithAccounts} from './api-client';
import {ApplicationService} from './application.service';
import {patchState, signalState} from '@ngrx/signals';
import {UserEntity} from '../entities/user-entity';
import moment from 'moment';
import {toObservable} from '@angular/core/rxjs-interop';
import {combineLatest, map, NEVER, switchMap, tap} from 'rxjs';

interface ProviderStore {
  users: UserEntity[];
  page: number;
  totalPages: number;
}

@Injectable({
  providedIn: 'root',
})
export class UserService {
  private readonly apiClient = inject(ApiClient);
  private readonly applicationService = inject(ApplicationService);

  private readonly pageSize = 20;

  private readonly store$$ = signalState<ProviderStore>({
    users: [],
    page: 0,
    totalPages: 0,
  });

  readonly users$ = toObservable(this.store$$.users);
  readonly page$ = toObservable(this.store$$.page);
  readonly totalPages$ = toObservable(this.store$$.totalPages);

  readonly loadUsers$ = combineLatest([
    this.applicationService.selectedApplication$,
    this.page$,
  ]).pipe(
    switchMap(([application, page]) => {
      if (application)
        return this.loadUsers(application.id, page);
      return NEVER;
    }),
  );

  selectPage(index: number) {
    patchState(this.store$$, {
      page: index,
    })
  }

  deleteUser(userId: string) {
    return this.applicationService.selectedApplication$.pipe(
      switchMap(application => {
        if (application)
          return this.apiClient.usersDELETE2(application.id, userId).pipe(
            switchMap(() => this.loadUsers(application.id, this.store$$.page()))
          );
        return NEVER;
      }),
    );
  }

  private loadUsers(applicationId: string, page: number) {
    return this.apiClient.usersGET2(applicationId, page, this.pageSize).pipe(
      tap(resp => {
        const totalPages = Math.floor((resp.total - 1) / this.pageSize) + 1;
        patchState(this.store$$, {totalPages});
      }),
      map(resp => resp.users?.map(userInfoToEntity) ?? []),
      tap(users => {
        patchState(this.store$$, {
          users
        });
      }),
      switchMap(() => NEVER),
    )
  }

}

const userInfoToEntity = (user: UserWithAccounts): UserEntity => {
  if (user.id === undefined)
    throw new Error("Provider ID is required");
  return {
    id: user.id,
    createdAt: user.createdAt ?? moment(),
    deletedAt: user.deletedAt ?? null,
    accounts: user.accounts?.map(account => {
      return {
        id: account.providerId,
        providerId: account.providerId,
        name: account.userInfo.name,
        email: account.userInfo.email,
        avatarUrl: account.userInfo.avatarUrl,
      }
    }) ?? [],
  }
}
