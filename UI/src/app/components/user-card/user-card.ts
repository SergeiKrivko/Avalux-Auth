import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject, input, OnInit} from '@angular/core';
import {UserEntity} from '../../entities/user-entity';
import {ProviderService} from '../../services/provider.service';
import {map, Subject, tap} from 'rxjs';
import {TuiLabel, TuiTextfield} from '@taiga-ui/core';
import {TuiCard} from '@taiga-ui/layout';
import {TuiAvatar, TuiChevron, TuiCopy, TuiDataListWrapper, TuiSelect} from '@taiga-ui/kit';
import {AsyncPipe} from '@angular/common';
import {FormControl, ReactiveFormsModule} from '@angular/forms';
import {ProviderEntity} from '../../entities/provider-entity';
import {ProviderInfoPipe} from '../../pipes/provider-info-pipe';
import {TuiLet} from '@taiga-ui/cdk';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';

@Component({
  selector: 'app-user-card',
  imports: [
    TuiLabel,
    TuiCard,
    TuiTextfield,
    TuiChevron,
    TuiSelect,
    TuiDataListWrapper,
    AsyncPipe,
    ReactiveFormsModule,
    ProviderInfoPipe,
    TuiLet,
    TuiCopy,
    TuiAvatar
  ],
  templateUrl: './user-card.html',
  styleUrl: './user-card.scss',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UserCard implements OnInit {
  private readonly providerService = inject(ProviderService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly changeDetectorRef = inject(ChangeDetectorRef);

  user = input.required<UserEntity>();

  protected readonly control = new FormControl<ProviderEntity | null>(null);
  private readonly selectedProvider = new Subject<ProviderEntity | null>();

  ngOnInit() {
    this.control.valueChanges.pipe(
      tap(value => this.selectedProvider.next(value)),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe();
  }

  protected readonly providers$ = this.providerService.addedProviders$.pipe(
    map(providers => providers
      .filter(p => this.user().accounts.find(a => a.providerId == p.id))
    ),
    tap(providers => {
      if (!this.control.value) {
        this.control.setValue(providers[0]);
        this.selectedProvider.next(providers[0]);
        this.changeDetectorRef.detectChanges();
      }
    })
  );

  protected selectedAccount$ = this.selectedProvider.pipe(
    map(provider => {
      console.log(provider);
      if (!provider)
        return null;
      return this.user().accounts.find(a => a.providerId == provider.id);
    }),
  );
}
