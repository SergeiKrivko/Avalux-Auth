import {ChangeDetectionStrategy, Component, DestroyRef, forwardRef, inject, OnInit} from '@angular/core';
import {TuiGroup, TuiTextfield} from '@taiga-ui/core';
import {TuiChevron, TuiDataListWrapper, TuiSelect} from '@taiga-ui/kit';
import {ControlValueAccessor, FormControl, FormGroup, NG_VALUE_ACCESSOR, ReactiveFormsModule} from '@angular/forms';
import {tap} from 'rxjs';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {CurrencyEntity, MoneyEntity} from '../../entities/money-entity';

@Component({
  selector: 'app-input-money',
  imports: [
    TuiGroup,
    TuiTextfield,
    TuiChevron,
    TuiSelect,
    TuiDataListWrapper,
    ReactiveFormsModule
  ],
  templateUrl: './input-money.html',
  styleUrl: './input-money.scss',
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => InputMoney),
      multi: true
    }
  ],
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InputMoney implements OnInit, ControlValueAccessor {
  private readonly destroyRef = inject(DestroyRef);

  protected readonly currencies: CurrencyEntity[] = [
    {key: 'RUB', displayName: '₽'},
    {key: 'USD', displayName: '$'},
    {key: 'EUR', displayName: '€'},
  ];

  protected readonly control = new FormGroup({
    amount: new FormControl<number>(0),
    currency: new FormControl<CurrencyEntity>(this.currencies[0]),
  });

  protected stringify(cur: CurrencyEntity): string {
    return cur.displayName;
  }


  ngOnInit() {
    this.control.valueChanges.pipe(
      tap(() => this.onChange(this.readValue())),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe();
  }

  private onChange: (value: MoneyEntity) => void = () => {
  };
  private onTouched: () => void = () => {
  };

  private readValue(): MoneyEntity {
    return {
      amount: this.control.value.amount ?? 0,
      currency: this.control.value.currency?.key ?? this.currencies[0].key,
    };
  }

  writeValue(value: MoneyEntity): void {
    this.control.setValue({
      amount: value.amount,
      currency: this.currencies.find(e => e.key == value.currency) ?? null,
    });
  }

  registerOnChange(fn: (value: MoneyEntity) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState?(isDisabled: boolean): void {
    if (isDisabled)
      this.control.disable();
    else
      this.control.enable();
  }
}
