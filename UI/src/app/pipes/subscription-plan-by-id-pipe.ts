import {inject, Pipe, PipeTransform} from '@angular/core';
import {SubscriptionService} from '../services/subscription.service';
import {map, Observable} from 'rxjs';
import {SubscriptionPlanEntity} from '../entities/subscription-entity';

@Pipe({
  name: 'subscriptionPlanById',
  standalone: true
})
export class SubscriptionPlanByIdPipe implements PipeTransform {
  private readonly subscriptionService = inject(SubscriptionService);

  transform(value: string): Observable<SubscriptionPlanEntity | undefined> {
    return this.subscriptionService.plans$.pipe(
      map(plans => plans.find(e => e.id == value)),
    );
  }

}
