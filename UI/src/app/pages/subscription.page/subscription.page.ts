import {ChangeDetectionStrategy, Component, DestroyRef, inject, OnInit} from '@angular/core';
import {AsyncPipe} from '@angular/common';
import {FormControl, FormGroup, FormsModule, ReactiveFormsModule} from '@angular/forms';
import {InputStringArray} from '../../components/input-string-array/input-string-array';
import {ActivatedRoute, RouterLink} from '@angular/router';
import {TuiButton, TuiLabel, TuiTextfieldComponent, TuiTextfieldDirective} from '@taiga-ui/core';
import {TuiButtonLoading, TuiSwitch, TuiTextarea} from '@taiga-ui/kit';
import {SubscriptionService} from '../../services/subscription.service';
import {NEVER, Subject, switchMap, tap} from 'rxjs';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {SubscriptionPlanInfoEntity} from '../../entities/subscription-entity';
import {InputMoney} from '../../components/input-money/input-money';
import {MoneyEntity} from '../../entities/money-entity';
import {EditorComponent, NGX_MONACO_EDITOR_CONFIG} from 'ngx-monaco-editor-v2';

@Component({
  selector: 'app-subscription.page',
  imports: [
    AsyncPipe,
    FormsModule,
    InputStringArray,
    ReactiveFormsModule,
    RouterLink,
    TuiButton,
    TuiButtonLoading,
    TuiLabel,
    TuiSwitch,
    TuiTextfieldComponent,
    TuiTextfieldDirective,
    TuiTextarea,
    InputMoney,
    EditorComponent,
  ],
  providers: [{
    provide: NGX_MONACO_EDITOR_CONFIG,
    useValue: {
      baseUrl: 'https://unpkg.com/monaco-editor@latest/min/vs',
      defaultOptions: {scrollBeyondLastLine: false}
    }
  }],
  templateUrl: './subscription.page.html',
  styleUrl: './subscription.page.scss',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SubscriptionPage implements OnInit {
  private readonly subscriptionService = inject(SubscriptionService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly selectedPlan$ = this.subscriptionService.selectedPlan$;

  protected readonly control = new FormGroup({
    key: new FormControl<string>(""),
    displayName: new FormControl<string>(""),
    description: new FormControl<string>(""),
    advantages: new FormControl<string[]>([]),
    isHidden: new FormControl<boolean>(false),
    isDefault: new FormControl<boolean>(false),
    price: new FormControl<MoneyEntity>({amount: 0, currency: 'RUB'}),
    data: new FormControl<string>(""),
  })

  protected isNew: boolean = false;

  editorOptions = {
    language: 'json',
    automaticLayout: true,
    minimap: {enabled: false},
    formatOnPaste: true,
    formatOnType: true,
    scrollBeyondLastLine: false,
    wordWrap: 'on',
    theme: 'vs-dark',
    lineNumbers: false,
  };

  ngOnInit() {
    this.route.params.pipe(
      switchMap(params => {
        const planId = params['planId'];
        this.isNew = planId === 'new';
        console.log(planId)
        if (planId && planId != 'new')
          return this.subscriptionService.selectPlan(planId);
        return NEVER;
      }),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe();

    this.selectedPlan$.pipe(
      tap(subscription => {
        if (subscription)
          this.loadSubscription(subscription?.info);
      }),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe();
  }

  private loadSubscription(planInfo: SubscriptionPlanInfoEntity) {
    this.control.setValue({
      key: planInfo.key ?? "",
      displayName: planInfo.displayName ?? "",
      description: planInfo.description ?? "",
      advantages: planInfo.advantages,
      isHidden: planInfo.isHidden,
      isDefault: planInfo.isDefault,
      price: planInfo.price,
      data: JSON.stringify(planInfo.data, null, 4),
    });
  }

  protected isSaving = new Subject<boolean>();

  private readValue(): SubscriptionPlanInfoEntity {
    return {
      key: this.control.value.key ?? "",
      displayName: this.control.value.displayName ?? "",
      description: this.control.value.description ?? "",
      advantages: this.control.value.advantages ?? [],
      isHidden: this.control.value.isHidden ?? false,
      isDefault: this.control.value.isDefault ?? false,
      price: this.control.value.price ?? {amount: 0, currency: 'RUB'},
      data: JSON.parse(this.control.value.data ?? ""),
    };
  }

  protected createSubscription() {
    this.isSaving.next(true);
    return this.subscriptionService.createNewPlan(this.readValue()).pipe(
      tap(() => {
        this.isSaving.next(false);
      }),
    ).subscribe();
  }

  protected updateSubscription() {
    this.isSaving.next(true);
    return this.subscriptionService.updatePlan(this.readValue()).pipe(
      tap(() => {
        this.isSaving.next(false);
      }),
    ).subscribe();
  }
}
