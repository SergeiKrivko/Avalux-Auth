import {ChangeDetectionStrategy, Component, DestroyRef, inject, input, OnInit} from '@angular/core';
import {UserEntity} from '../../entities/user-entity';
import {ProviderService} from '../../services/provider.service';
import {map, tap} from 'rxjs';
import {TuiButton, TuiTextfield} from '@taiga-ui/core';
import {TuiCard} from '@taiga-ui/layout';
import {TuiAvatar, TuiChevron, TuiCopy, TuiDataListWrapper, TuiSelect} from '@taiga-ui/kit';
import {AsyncPipe} from '@angular/common';
import {FormControl, ReactiveFormsModule} from '@angular/forms';
import {ProviderEntity} from '../../entities/provider-entity';
import {ProviderInfoPipe} from '../../pipes/provider-info-pipe';
import {TuiLet} from '@taiga-ui/cdk';
import {takeUntilDestroyed, toObservable} from '@angular/core/rxjs-interop';
import {patchState, signalState} from '@ngrx/signals';
import {UserService} from '../../services/user.service';

interface Store {
  provider: ProviderEntity | null;
}

@Component({
  selector: 'app-user-card',
  imports: [
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
    TuiAvatar,
    TuiButton
  ],
  templateUrl: './user-card.html',
  styleUrl: './user-card.scss',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UserCard implements OnInit {
  private readonly providerService = inject(ProviderService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly userService = inject(UserService);

  user = input.required<UserEntity>();

  protected readonly control = new FormControl<ProviderEntity | null>(null);
  private readonly store$$ = signalState<Store>({provider: null});

  ngOnInit() {
    this.control.valueChanges.pipe(
      tap(value => patchState(this.store$$, {provider: value})),
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
        patchState(this.store$$, {provider: providers[0]})
      }
    })
  );

  protected selectedAccount$ = toObservable(this.store$$.provider).pipe(
    map(provider => {
      if (!provider)
        return null;
      return this.user().accounts.find(a => a.providerId == provider.id);
    }),
  );

  protected deleteUser() {
    this.userService.deleteUser(this.user().id).pipe(
      takeUntilDestroyed(this.destroyRef),
    ).subscribe();
  }
}
