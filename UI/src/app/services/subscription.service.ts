import {inject, Injectable} from '@angular/core';
import {
  SubscriptionPlanEntity,
  SubscriptionPlanInfoEntity
} from '../entities/subscription-entity';
import {
  AddSubscriptionRequestSchema,
  ApiClient,
  SubscriptionPlan, SubscriptionPlanInfo
} from './api-client';
import {Moment} from 'moment';
import {ApplicationService} from './application.service';
import {patchState, signalState} from '@ngrx/signals';
import {toObservable} from '@angular/core/rxjs-interop';
import {combineLatest, first, map, NEVER, switchMap, tap} from 'rxjs';

interface SubscriptionStore {
  plans: SubscriptionPlanEntity[],
  selectedPlan: SubscriptionPlanEntity | null;
  isLoaded: boolean;
}

@Injectable({
  providedIn: 'root',
})
export class SubscriptionService {
  private readonly apiClient = inject(ApiClient);
  private readonly applicationService = inject(ApplicationService);

  private readonly store$$ = signalState<SubscriptionStore>({
    plans: [],
    selectedPlan: null,
    isLoaded: false,
  });

  readonly plans$ = toObservable(this.store$$.plans);
  readonly selectedPlan$ = toObservable(this.store$$.selectedPlan);

  readonly loadPlansOnApplicationChange$ = this.loadPlans().pipe(
    switchMap(() => NEVER),
  );

  private loadPlans() {
    return this.applicationService.selectedApplication$.pipe(
      switchMap(application => {
        if (application)
          return this.apiClient.subscriptionsAll(application.id)
        return NEVER;
      }),
      map(tokens => tokens.map(planToEntity)),
      tap(plans => patchState(this.store$$, {plans})),
    )
  }

  createNewPlan(info: SubscriptionPlanInfoEntity) {
    return this.applicationService.selectedApplication$.pipe(
      switchMap(application => {
        if (application)
          return this.apiClient.subscriptionsPOST(application.id, SubscriptionPlanInfo.fromJS(info));
        return NEVER;
      }),
      switchMap(_ => this.loadPlans()),
    );
  }

  updatePlan(info: SubscriptionPlanInfoEntity) {
    return combineLatest([this.applicationService.selectedApplication$, this.selectedPlan$]).pipe(
      first(),
      switchMap(([app, plan]) => {
        if (plan && app)
          return this.apiClient.subscriptionsPUT(app.id, plan.id, SubscriptionPlanInfo.fromJS(info));
        return NEVER;
      })
    )
  }

  selectPlan(planId: string) {
    return this.plans$.pipe(
      first(),
      map(plans => {
        if (!plans)
          return false;
        const plan = plans.find(p => p.id === planId);
        if (!plan)
          return false;
        patchState(this.store$$, {selectedPlan: plan});
        return true;
      }),
    );
  }

  giveSubscription(userId: string, planId: string, expiresAt: Moment) {
    return this.applicationService.selectedApplication$.pipe(
      first(),
      switchMap(app => {
        if (!app)
          return NEVER;
        return this.apiClient.subscriptionsPOST2(app.id, userId, AddSubscriptionRequestSchema.fromJS({
          planId,
          expiresAt
        }));
      }),
    );
  }
}

const planToEntity = (plan: SubscriptionPlan): SubscriptionPlanEntity => {
  if (plan.id === null || plan.info.key === undefined)
    throw new Error("Plan ID is required");
  return {
    id: plan.id,
    applicationId: plan.applicationId,
    info: {
      key: plan.info.key,
      displayName: plan.info.displayName,
      description: plan.info.description,
      advantages: plan.info.advantages ?? [],
      isDefault: plan.info.isDefault ?? false,
      isHidden: plan.info.isHidden ?? false,
      price: {amount: plan.info.price.amount, currency: plan.info.price.currency ?? 'RUB'},
      data: plan.info.data,
    },
    createdAt: plan.createdAt,
  }
}
