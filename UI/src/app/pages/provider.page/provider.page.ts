import {ChangeDetectionStrategy, Component, DestroyRef, inject, OnInit} from '@angular/core';
import {ActivatedRoute, RouterLink} from '@angular/router';
import {first, NEVER, of, Subject, switchMap, tap} from 'rxjs';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {ProviderService} from '../../services/provider.service';
import {TuiButton, TuiDialogService, TuiTextfield} from '@taiga-ui/core';
import {FormControl, FormGroup, ReactiveFormsModule} from '@angular/forms';
import {TuiLet} from '@taiga-ui/cdk';
import {AsyncPipe} from '@angular/common';
import {TUI_CONFIRM, TuiButtonLoading, TuiConfirmData, TuiCopy, TuiSwitch} from '@taiga-ui/kit';
import {InputStringArray} from '../../components/input-string-array/input-string-array';
import {ProviderEntity} from '../../entities/provider-entity';
import {ProviderRedirectUrlPipe} from '../../pipes/provider-redirect-url-pipe';

@Component({
  selector: 'app-provider.page',
  imports: [
    RouterLink,
    TuiButton,
    TuiTextfield,
    ReactiveFormsModule,
    TuiLet,
    AsyncPipe,
    TuiCopy,
    TuiSwitch,
    TuiButtonLoading,
    ProviderRedirectUrlPipe,
    InputStringArray
  ],
  templateUrl: './provider.page.html',
  styleUrl: './provider.page.scss',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProviderPage implements OnInit {
  private readonly providerService = inject(ProviderService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  private readonly dialogService = inject(TuiDialogService);

  protected readonly selectedProvider$ = this.providerService.selectedProvider$;
  protected readonly selectedProviderInfo$ = this.providerService.selectedProvider$.pipe(
    switchMap(e => {
      if (e)
        return this.providerService.providerInfoById(e.providerId);
      return of(null);
    }),
  );

  protected readonly control = new FormGroup({
    clientName: new FormControl<string>(""),
    clientId: new FormControl<string>(""),
    clientSecret: new FormControl<string>(""),
    providerUrl: new FormControl<string>(""),
    saveTokens: new FormControl<boolean>(false),
    defaultScope: new FormControl<string[]>([]),
  })

  ngOnInit() {
    this.route.params.pipe(
      switchMap(params => {
        const appId = params['providerId'];
        if (appId)
          return this.providerService.providerById(appId);
        return NEVER;
      }),
      tap(provider => {
          if (provider)
            this.providerService.selectProvider(provider);
        }
      ),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe();

    this.selectedProvider$.pipe(
      tap(provider => {
        if (provider)
          this.loadProvider(provider);
      }),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe();
  }

  private loadProvider(provider: ProviderEntity) {
    this.control.setValue({
      clientName: provider.parameters.clientName ?? "",
      clientId: provider.parameters.clientId ?? "",
      clientSecret: provider.parameters.clientSecret ?? "",
      providerUrl: provider.parameters.providerUrl ?? "",
      saveTokens: provider.parameters.saveTokens,
      defaultScope: provider.parameters.defaultScope,
    })
  }

  protected isSaving = new Subject<boolean>();

  protected saveChanges() {
    this.isSaving.next(true);
    this.selectedProvider$.pipe(
      first(),
      switchMap(provider => {
        if (provider)
          return this.providerService.updateProvider(provider.id, {
            clientName: this.control.value.clientName ?? "",
            clientId: this.control.value.clientId ?? "",
            clientSecret: this.control.value.clientSecret ?? "",
            providerUrl: this.control.value.providerUrl ?? "",
            saveTokens: this.control.value.saveTokens ?? false,
            defaultScope: this.control.value.defaultScope ?? [],
          });
        return of(null);
      }),
      tap(() => {
        this.isSaving.next(false);
      }),
    ).subscribe();
  }

  protected deleteProvider() {
    const data: TuiConfirmData = {
      content: 'Вы уверены, что хотите отключить провайдер?' +
        'Пользователи, не авторизовавшиеся ни через один из других провайдеров, могут потерять доступ к аккаунту.',
      yes: 'Да',
      no: 'Нет',
      appearance: 'primary-destructive',
    };
    this.dialogService
      .open<boolean>(TUI_CONFIRM, {
        label: 'Отключение провайдера',
        size: 's',
        data,
      }).pipe(
      switchMap(flag => {
        if (flag)
          return this.selectedProvider$;
        return of(null);
      }),
      first(),
      switchMap(provider => {
        if (provider)
          return this.providerService.deleteProvider(provider.id);
        return of(null);
      }),
      tap(() => {
        this.isSaving.next(false);
      }),
    ).subscribe();
  }
}
