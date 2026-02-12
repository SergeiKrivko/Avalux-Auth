import {ChangeDetectionStrategy, Component, inject} from '@angular/core';
import {TuiButton, tuiDialog, TuiSurface} from '@taiga-ui/core';
import {AsyncPipe} from '@angular/common';
import {TuiCardLarge, TuiHeader} from '@taiga-ui/layout';
import {RouterLink} from '@angular/router';
import {ApplicationService} from '../../services/application.service';
import {NewApplicationDialog} from '../../components/new-application-dialog/new-application-dialog';

@Component({
  selector: 'app-home.page',
  imports: [
    TuiButton,
    AsyncPipe,
    TuiCardLarge,
    TuiHeader,
    TuiSurface,
    RouterLink
  ],
  templateUrl: './home.page.html',
  styleUrl: './home.page.scss',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HomePage {
  private readonly applicationService = inject(ApplicationService);

  protected readonly applications$ = this.applicationService.applications$;

  private readonly newAppDialog = tuiDialog(NewApplicationDialog, {
    dismissible: false,
    label: 'Новое приложение',
  });

  protected newApp() {
    this.newAppDialog(undefined).subscribe();
  }
}
