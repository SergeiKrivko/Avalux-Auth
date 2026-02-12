import {ChangeDetectionStrategy, Component, inject} from '@angular/core';
import {TuiButton, TuiDialogContext, TuiLabel, TuiTextfield} from '@taiga-ui/core';
import {injectContext} from '@taiga-ui/polymorpheus';
import {TuiButtonLoading, TuiTextarea} from '@taiga-ui/kit';
import {ApplicationService} from '../../services/application.service';
import {FormControl, FormGroup, ReactiveFormsModule} from '@angular/forms';
import {first, tap} from 'rxjs';

@Component({
  standalone: true,
  selector: 'app-new-application-dialog',
  imports: [
    TuiLabel,
    TuiTextfield,
    TuiTextarea,
    TuiButton,
    TuiButtonLoading,
    ReactiveFormsModule
  ],
  templateUrl: './new-application-dialog.html',
  styleUrl: './new-application-dialog.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class NewApplicationDialog {
  private readonly applicationService = inject(ApplicationService);

  private readonly context = injectContext<TuiDialogContext>();

  protected readonly control = new FormGroup({
    name: new FormControl<string>(""),
  })

  protected loading: boolean = false;

  protected submit() {
    if (this.control.value.name === null)
      return;
    this.loading = true;
    this.applicationService.createNewApplication(this.control.value.name ?? "").pipe(
        tap(() => {
          this.context.completeWith();
          this.loading = false;
        }),
        first(),
    ).subscribe();
  }
}
