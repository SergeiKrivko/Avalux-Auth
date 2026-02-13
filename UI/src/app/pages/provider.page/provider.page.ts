import {ChangeDetectionStrategy, Component, DestroyRef, inject, signal, Signal} from '@angular/core';
import {ActivatedRoute, RouterLink} from '@angular/router';
import {first, NEVER, of, Subject, switchMap, tap} from 'rxjs';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {ProviderService} from '../../services/provider.service';
import {TuiButton, TuiTextfield} from '@taiga-ui/core';
import {FormArray, FormControl, FormGroup, ReactiveFormsModule} from '@angular/forms';
import {TuiLet} from '@taiga-ui/cdk';
import {AsyncPipe} from '@angular/common';
import {TuiButtonLoading, TuiCopy, TuiSwitch} from '@taiga-ui/kit';
import {InputStringArray} from '../../components/input-string-array/input-string-array';
import {ProviderEntity} from '../../entities/provider-entity';

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
    TuiButtonLoading
  ],
  templateUrl: './provider.page.html',
  styleUrl: './provider.page.scss',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProviderPage {
  private readonly providerService = inject(ProviderService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly selectedProvider$ = this.providerService.selectedProvider$;
  protected readonly selectedProviderInfo$ = this.providerService.selectedProvider$.pipe(
    switchMap(e => {
      if (e)
        return this.providerService.providerInfoById(e.providerId);
      return of(null);
    }),
  );

  protected readonly redirectUrl = `${window.location.protocol}//${window.location.host}/api/v1/auth/yandex/callback`

  protected readonly control = new FormGroup({
    clientId: new FormControl<string>(""),
    clientSecret: new FormControl<string>(""),
    saveTokens: new FormControl<boolean>(false),
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
      clientId: provider.parameters.clientId ?? "",
      clientSecret: provider.parameters.clientSecret ?? "",
      saveTokens: provider.parameters.saveTokens,
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
            clientId: this.control.value.clientId ?? "",
            clientSecret: this.control.value.clientSecret ?? "",
            saveTokens: this.control.value.saveTokens ?? false,
          });
        return of(null);
      }),
      tap(() => {
        this.isSaving.next(false);
      }),
    ).subscribe();
  }
}
