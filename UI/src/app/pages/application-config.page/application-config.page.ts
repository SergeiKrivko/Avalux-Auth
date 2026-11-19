import {ChangeDetectionStrategy, Component, DestroyRef, inject, OnInit, signal} from '@angular/core';
import {ApplicationService} from '../../services/application.service';
import {Router} from '@angular/router';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {catchError, first, NEVER, Subject, switchMap, tap} from 'rxjs';
import {ApplicationEntity} from '../../entities/application-entity';
import {TuiResponsiveDialogService} from '@taiga-ui/addon-mobile';
import {FormControl, FormGroup, FormsModule, ReactiveFormsModule} from '@angular/forms';
import {TUI_CONFIRM, TuiButtonLoading, TuiConfirmData, TuiCopy} from '@taiga-ui/kit';
import {TuiButton, TuiIcon, TuiLabel, TuiTextfieldComponent, TuiTextfieldDirective} from '@taiga-ui/core';
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
    TuiTextfieldComponent,
    TuiTextfieldDirective,
    TuiCopy,
    TuiLet,
    AsyncPipe,
    InputStringArray,
    TuiButton,
    TuiButtonLoading,
    TuiIcon

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
    name: new FormControl<string>(""),
    redirectUrls: new FormControl<string[]>([]),
    requireClientSecret: new FormControl<boolean>(true),
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

  /**
   * Signature of what is currently in the form. The selected application is
   * re-emitted when the icon changes (the header and the list must update), and
   * reloading the form then would drop the edits the user has not saved yet.
   */
  private loadedSignature: string | null = null;

  private loadApplication(application: ApplicationEntity) {
    const value = {
      name: application.parameters.name,
      redirectUrls: application.parameters.redirectUrls,
      requireClientSecret: application.parameters.requireClientSecret,
    };

    const signature = JSON.stringify([application.id, value]);
    if (signature === this.loadedSignature)
      return;

    this.loadedSignature = signature;
    this.control.setValue(value);
  }

  protected isSaving = new Subject<boolean>();

  /** Icon upload state; the icon is stored in S3 the moment it is chosen. */
  protected readonly isIconUploading = signal(false);
  protected readonly iconError = signal<string | null>(null);
  private readonly maxIconBytes = 5 * 1024 * 1024;

  protected onIconSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    // Reset the input so picking the same file again still fires a change event.
    input.value = '';
    if (file)
      this.uploadIcon(file);
  }

  private uploadIcon(file: File) {
    this.iconError.set(null);

    if (!file.type.startsWith('image/')) {
      this.iconError.set('Можно загрузить только изображение');
      return;
    }
    if (file.size > this.maxIconBytes) {
      this.iconError.set('Файл больше 5 МБ');
      return;
    }

    this.isIconUploading.set(true);
    this.selectedApplication$.pipe(
      first(),
      switchMap(app => app ? this.applicationService.uploadIcon(app.id, file) : NEVER),
      tap(() => this.isIconUploading.set(false)),
      catchError(() => {
        this.isIconUploading.set(false);
        this.iconError.set('Не удалось загрузить иконку');
        return NEVER;
      }),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe();
  }

  protected removeIcon(): void {
    const data: TuiConfirmData = {
      content: 'Удалить иконку приложения?',
      yes: 'Удалить',
      no: 'Отмена',
    };

    this.dialogs
      .open<boolean>(TUI_CONFIRM, {
        label: 'Удаление иконки',
        size: 's',
        data,
      })
      .pipe(
        switchMap(result => {
          if (!result)
            return NEVER;
          return this.selectedApplication$.pipe(
            first(),
            switchMap(app => app ? this.applicationService.deleteIcon(app.id) : NEVER),
          );
        }),
        catchError(() => {
          this.iconError.set('Не удалось удалить иконку');
          return NEVER;
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe();
  }

  protected saveChanges() {
    this.isSaving.next(true);
    this.selectedApplication$.pipe(
      first(),
      switchMap(app => {
        if (app)
          return this.applicationService.updateApplication(app.id, {
            name: this.control.value.name ?? app.parameters.name,
            redirectUrls: this.control.value.redirectUrls ?? [],
            requireClientSecret: this.control.value.requireClientSecret ?? true,
          });
        return NEVER;
      }),
      tap(() => this.isSaving.next(false)),
      catchError(() => {
        this.isSaving.next(false);
        return NEVER;
      }),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe();
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
