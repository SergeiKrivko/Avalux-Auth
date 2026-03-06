import {ChangeDetectionStrategy, Component, inject} from '@angular/core';
import {TuiButton, TuiCalendar, TuiDialogContext, TuiLabel, TuiTextfield} from '@taiga-ui/core';
import {injectContext} from '@taiga-ui/polymorpheus';
import {
  TuiButtonLoading,
  TuiChevron,
  TuiDataListWrapperComponent,
  TuiInputDateDirective,
  TuiSelectDirective
} from '@taiga-ui/kit';
import {FormControl, FormGroup, ReactiveFormsModule} from '@angular/forms';
import {first, tap} from 'rxjs';
import {SubscriptionService} from '../../services/subscription.service';
import {SubscriptionPlanEntity} from '../../entities/subscription-entity';
import {TuiDay} from '@taiga-ui/cdk';
import moment from 'moment/moment';
import {AsyncPipe} from '@angular/common';

@Component({
  standalone: true,
  selector: 'app-new-application-dialog',
  imports: [
    TuiLabel,
    TuiTextfield,
    TuiButton,
    TuiButtonLoading,
    ReactiveFormsModule,
    TuiChevron,
    TuiDataListWrapperComponent,
    TuiSelectDirective,
    AsyncPipe,
    TuiCalendar,
    TuiInputDateDirective
  ],
  templateUrl: './give-subscription-dialog.component.html',
  styleUrl: './give-subscription-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class GiveSubscriptionDialog {
  private readonly subscriptionService = inject(SubscriptionService);
  public readonly context = injectContext<TuiDialogContext<undefined, string>>();

  protected readonly plans$ = this.subscriptionService.plans$;

  protected readonly control = new FormGroup({
    plan: new FormControl<SubscriptionPlanEntity | undefined>(undefined),
    expiresAt: new FormControl<TuiDay | undefined>(undefined),
  })

  protected loading: boolean = false;

  protected stringify(plan: SubscriptionPlanEntity) {
    return plan.info.displayName ?? "";
  }

  protected submit() {
    if (this.control.value.plan === undefined || this.control.value.expiresAt === undefined)
      return;
    this.loading = true;
    console.log(this.context);
    this.subscriptionService.giveSubscription(this.context.data, this.control.value.plan?.id ?? "", moment(this.control.value.expiresAt?.toJSON())).pipe(
      tap(() => {
        this.context.completeWith(undefined);
        this.loading = false;
      }),
      first(),
    ).subscribe();
  }
}
