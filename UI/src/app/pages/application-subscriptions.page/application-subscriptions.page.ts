import {ChangeDetectionStrategy, Component, DestroyRef, inject, OnInit} from '@angular/core';
import {TuiButton, TuiScrollbar} from '@taiga-ui/core';
import {RouterLink} from '@angular/router';
import {SubscriptionService} from '../../services/subscription.service';
import {AsyncPipe} from '@angular/common';
import {TuiCard} from '@taiga-ui/layout';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {CurrencyEntity} from '../../entities/money-entity';
import {TuiInputRange} from '@taiga-ui/kit';
import {CurrencySymbolPipe} from '../../pipes/currency-symbol-pipe';

@Component({
  selector: 'app-application-subscriptions.page',
  imports: [
    TuiButton,
    RouterLink,
    TuiScrollbar,
    AsyncPipe,
    TuiCard,
    TuiInputRange,
    CurrencySymbolPipe
  ],
  templateUrl: './application-subscriptions.page.html',
  styleUrl: './application-subscriptions.page.scss',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ApplicationSubscriptionsPage implements OnInit {
  private readonly subscriptionService = inject(SubscriptionService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly plans$ = this.subscriptionService.plans$;

  ngOnInit() {
    this.subscriptionService.loadPlansOnApplicationChange$.pipe(
      takeUntilDestroyed(this.destroyRef),
    ).subscribe();
  }

}
