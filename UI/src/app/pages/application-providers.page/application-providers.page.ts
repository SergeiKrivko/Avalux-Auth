import {ChangeDetectionStrategy, Component, DestroyRef, inject} from '@angular/core';
import {ProviderService} from '../../services/provider.service';
import {combineLatest, from, map, Observable, switchMap, tap} from 'rxjs';
import {AsyncPipe} from '@angular/common';
import {TuiCardLarge} from '@taiga-ui/layout';
import {ProviderEntity, ProviderInfoEntity} from '../../entities/provider-entity';
import {TuiButton} from '@taiga-ui/core';
import {Router, RouterLink} from '@angular/router';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';

interface ProviderPair {
  info: ProviderInfoEntity | undefined;
  provider: ProviderEntity;
}

@Component({
  selector: 'app-application-providers.page',
  imports: [
    AsyncPipe,
    TuiCardLarge,
    TuiButton,
    RouterLink
  ],
  templateUrl: './application-providers.page.html',
  styleUrl: './application-providers.page.scss',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ApplicationProvidersPage {
  private readonly providerService = inject(ProviderService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly addedProviders$: Observable<ProviderPair[]> = combineLatest([
    this.providerService.addedProviders$,
    this.providerService.providerInfos$,
  ]).pipe(
    map(([added, infos]) => added
      .map(e => {
        return {
          provider: e,
          info: infos.find(x => x.id === e.providerId),
        }
      })
      .filter(e => e.info !== undefined)
    ),
  );

  protected readonly availableProviders$: Observable<ProviderInfoEntity[]> = combineLatest([
    this.providerService.addedProviders$,
    this.providerService.providerInfos$,
  ]).pipe(
    map(([added, infos]) => infos.filter(e => added.find(x => x.providerId === e.id) === undefined)),
  );

  protected createNewProvider(id: number) {
    this.providerService.createNewProvider(id).pipe(
      switchMap(providerId => from(this.router.navigate(["./" + providerId]))),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe();
  }
}
