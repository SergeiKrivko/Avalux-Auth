import {ChangeDetectionStrategy, Component, DestroyRef, inject, OnInit} from '@angular/core';
import {ApplicationService} from '../../services/application.service';
import {Router} from '@angular/router';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {tap} from 'rxjs';
import {ApplicationEntity} from '../../entities/application-entity';
import {TuiResponsiveDialogService} from '@taiga-ui/addon-mobile';
import {FormGroup, FormsModule, ReactiveFormsModule} from '@angular/forms';
import {TUI_CONFIRM, TuiConfirmData, TuiCopy, TuiSwitch} from '@taiga-ui/kit';
import {TuiLabel, TuiTextfieldComponent, TuiTextfieldDirective} from '@taiga-ui/core';
import {TuiLet} from '@taiga-ui/cdk';
import {AsyncPipe} from '@angular/common';
import {InputStringArray} from '../../components/input-string-array/input-string-array';

@Component({
  standalone: true,
  selector: 'app-application-page',
  imports: [
    FormsModule,
    ReactiveFormsModule,
    TuiLabel,
    TuiSwitch,
    TuiTextfieldComponent,
    TuiTextfieldDirective,
    TuiCopy,
    TuiLet,
    AsyncPipe,
    InputStringArray

  ],
  templateUrl: './application-config.page.html',
  styleUrl: './application-config.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ApplicationConfigPage implements OnInit {
  private readonly applicationService = inject(ApplicationService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly dialogs = inject(TuiResponsiveDialogService);
  private readonly router = inject(Router);

  protected readonly selectedApplication$  = this.applicationService.selectedApplication$;

  protected control = new FormGroup({
  })

  ngOnInit() {
    this.applicationService.selectedApplication$.pipe(
      tap(app => {
        if (app)
          this.loadApplication(app);
      }),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe();
  }

  private loadApplication(application: ApplicationEntity) {
    this.control.setValue({
    });
    this.control.disable();
  }

  protected isEditing: boolean = false;

  protected startEditing() {
    this.isEditing = true;
    this.control.enable();
  }

  protected saveChanges() {
    // this.isEditing = false;
    // this.control.disable();
    // if (this.applicationId)
    //   this.applicationService.updateApplication(
    //     this.applicationId,
    //     this.control.value.name ?? undefined,
    //     this.control.value.description ?? undefined,
    //     this.control.value.mainBranch ?? undefined,
    //     this.control.value.latestReleaseLifetime ?? null,
    //     this.control.value.releaseLifetime ?? null,
    //   ).subscribe();
  }

  protected cancelChanges() {
    // this.isEditing = false;
    // this.control.disable();
    // if (this.applicationId) {
    //   this.applicationService.applicationById(this.applicationId).pipe(
    //     tap(app => {
    //       if (app)
    //         this.loadApplication(app);
    //     }),
    //     first(),
    //   ).subscribe();
    // }
  }

  protected deleteApplication(): void {
    const data: TuiConfirmData = {
            content: 'Вы уверены, что хотите удалить приложение?',
            yes: 'Да',
            no: 'Нет',
        };

        this.dialogs
            .open<boolean>(TUI_CONFIRM, {
                label: 'Удаление приложения',
                size: 's',
                data,
            })
            .pipe(
              // switchMap(result => {
              //   if (result && this.applicationId) {
              //     void this.router.navigate([".."]);
              //     return this.applicationService.deleteApplication(this.applicationId);
              //   }
              //   return NEVER;
              // }),
            )
            .subscribe();
  }
}
