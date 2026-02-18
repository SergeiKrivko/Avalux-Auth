import {Component, DestroyRef, inject, OnInit} from '@angular/core';
import {RouterOutlet} from '@angular/router';
import {TuiButton} from '@taiga-ui/core';
import {AuthService} from '../../services/auth.service';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {Logo} from '../../components/logo/logo';
import {ApplicationService} from '../../services/application.service';
import {combineLatest} from 'rxjs';
import {ProviderService} from '../../services/provider.service';

@Component({
  selector: 'app-root.page',
  imports: [
    RouterOutlet,
    TuiButton,
    Logo,
  ],
  templateUrl: './root.page.html',
  styleUrl: './root.page.scss',
  standalone: true
})
export class RootPage implements OnInit {
  private readonly destroyRef = inject(DestroyRef);
  private readonly authService = inject(AuthService);
  private readonly applicationService = inject(ApplicationService);
  private readonly providerService = inject(ProviderService);

  ngOnInit() {
    combineLatest([
      this.applicationService.loadApplicationsOnAuthChange$,
      this.providerService.loadProvidersOnApplicationChange$,
    ]).pipe(
      takeUntilDestroyed(this.destroyRef),
    ).subscribe();
  }

  logOut(): void {
    this.authService.logOut().pipe(
      takeUntilDestroyed(this.destroyRef),
    ).subscribe();
  }
}
