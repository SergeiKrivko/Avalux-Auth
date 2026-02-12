import {ChangeDetectionStrategy, Component, DestroyRef, inject, OnInit} from '@angular/core';
import {ActivatedRoute, IsActiveMatchOptions, RouterLink, RouterLinkActive, RouterOutlet} from '@angular/router';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {NEVER, Observable, switchMap, tap} from 'rxjs';
import {ApplicationService} from '../../services/application.service';
import {TuiScrollbar} from '@taiga-ui/core';
import {TuiSegmented} from '@taiga-ui/kit';
import {ApplicationEntity} from '../../entities/application-entity';
import {AsyncPipe} from '@angular/common';

@Component({
  standalone: true,
  selector: 'app-application-page',
  imports: [
    RouterOutlet,
    TuiScrollbar,
    RouterLink,
    RouterLinkActive,
    TuiSegmented,
    AsyncPipe
  ],
  templateUrl: './application.page.html',
  styleUrl: './application.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ApplicationPage implements OnInit {
  private readonly applicationService = inject(ApplicationService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly selectedApplication$ = this.applicationService.selectedApplication$;

  protected readonly options: IsActiveMatchOptions = {
    matrixParams: 'ignored',
    queryParams: 'ignored',
    paths: 'subset',
    fragment: 'exact',
  };

  ngOnInit() {
    this.route.params.pipe(
      switchMap(params => {
        const appId = params['id'];
        if (appId)
          return this.applicationService.applicationById(appId);
        return NEVER;
      }),
      tap(app => {
          if (app)
            this.applicationService.selectApplication(app);
        }
      ),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe();
  }
}
