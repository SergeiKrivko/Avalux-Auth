import {ChangeDetectionStrategy, Component, DestroyRef, inject, OnInit, signal, Signal} from '@angular/core';
import {ActivatedRoute, RouterLink} from '@angular/router';
import {first, map, NEVER, of, Subject, switchMap, tap} from 'rxjs';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {ProviderService} from '../../services/provider.service';
import {TuiButton, TuiTextfield} from '@taiga-ui/core';
import {FormArray, FormControl, FormGroup, ReactiveFormsModule} from '@angular/forms';
import {TuiLet} from '@taiga-ui/cdk';
import {AsyncPipe} from '@angular/common';
import {TuiButtonLoading, TuiCopy, TuiSwitch} from '@taiga-ui/kit';
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
}
