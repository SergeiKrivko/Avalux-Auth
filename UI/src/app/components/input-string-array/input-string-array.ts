import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  forwardRef,
  inject,
  OnInit
} from '@angular/core';
import {
  ControlValueAccessor,
  FormArray,
  FormControl,
  NG_VALUE_ACCESSOR,
  ReactiveFormsModule,
  Validators
} from '@angular/forms';
import {tap} from 'rxjs';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {TuiButton, TuiTextfieldComponent, TuiTextfieldDirective} from '@taiga-ui/core';

@Component({
  selector: 'app-input-string-array',
  imports: [
    ReactiveFormsModule,
    TuiTextfieldComponent,
    TuiTextfieldDirective,
    TuiButton
  ],
  templateUrl: './input-string-array.html',
  styleUrl: './input-string-array.scss',
  standalone: true,
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => InputStringArray),
      multi: true
    }
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InputStringArray implements OnInit, ControlValueAccessor {
  private readonly destroyRef = inject(DestroyRef);
  private readonly changeDetectorRef = inject(ChangeDetectorRef);

  protected readonly control = new FormArray<FormControl<string | null>>([])

  ngOnInit() {
    this.control.valueChanges.pipe(
      tap(() => this.onChange(this.readValue())),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe();
  }

  private onChange: (value: string[] | null) => void = () => {
  };
  private onTouched: () => void = () => {
  };

  private readValue(): string[] | null {
    return this.control.value.map(e => e ?? "");
  }

  writeValue(value: string[] | null): void {
    this.control.clear();
    for (const url of value ?? []) {
      this.control.push(new FormControl(url));
    }
    this.control.setValue(value ?? []);
    this.changeDetectorRef.detectChanges();
  }

  registerOnChange(fn: (value: string[] | null) => void): void {
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

  protected addItem(): void {
    this.control.push(new FormControl<string>('', [Validators.required, Validators.minLength(1)]));
  }

  protected removeItem(index: number): void {
    this.control.removeAt(index);
  }
}
