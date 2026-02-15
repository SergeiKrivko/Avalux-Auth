import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject} from '@angular/core';
import {
  TuiButton,
  TuiLabel, TuiSelectLike,
  TuiTextfield,
  TuiTextfieldComponent,
  TuiTextfieldDirective
} from '@taiga-ui/core';
import {FormControl, FormGroup, ReactiveFormsModule} from '@angular/forms';
import {first, tap} from 'rxjs';
import {TokenService} from '../../services/token.service';
import moment from 'moment';
import {
  TuiButtonLoading,
  TuiChevron, TuiCopy,
  TuiDataListWrapper,
  TuiInputChip,
  TuiInputDate,
  TuiMultiSelect
} from '@taiga-ui/kit';
import {TuiDay, TuiLet} from '@taiga-ui/cdk';
import {AsyncPipe} from '@angular/common';
import {ProviderInfoPipe} from '../../pipes/provider-info-pipe';
import {TokenPermissionEntity} from '../../entities/token-entity';
import {TokenPermission} from '../../services/api-client';

@Component({
  selector: 'app-new-token-dialog',
  imports: [
    ReactiveFormsModule,
    TuiButton,
    TuiButtonLoading,
    TuiLabel,
    TuiTextfieldComponent,
    TuiTextfieldDirective,
    TuiInputDate,
    TuiTextfield,
    TuiChevron,
    TuiInputChip,
    TuiSelectLike,
    TuiDataListWrapper,
    TuiMultiSelect,
    AsyncPipe,
    ProviderInfoPipe,
    TuiLet,
    TuiCopy
  ],
  templateUrl: './new-token-dialog.html',
  styleUrl: './new-token-dialog.scss',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NewTokenDialog {
  private readonly tokenService = inject(TokenService);
  private readonly changeDetectorRef = inject(ChangeDetectorRef);

  protected readonly permissions$ = this.tokenService.permissions$;

  protected readonly control = new FormGroup({
    name: new FormControl<string>(""),
    expiresAt: new FormControl<TuiDay>(TuiDay.currentLocal()),
    permissions: new FormControl<TokenPermission[]>([]),
  });

  protected stringify(permission: TokenPermissionEntity) {
    return permission.key;
  }

  protected loading: boolean = false;
  protected token: string | undefined;

  protected submit() {
    if (this.control.value.name === null)
      return;
    this.loading = true;
    this.tokenService.createNewToken(this.control.value.name ?? "",
      this.control.value.permissions?.map(e => e.key ?? "") ?? [],
      moment(this.control.value.expiresAt?.toJSON())
    ).pipe(
      tap(token => {
        this.token = token;
        this.loading = false;
        this.changeDetectorRef.detectChanges();
      }),
      first(),
    ).subscribe();
  }
}
