import {ChangeDetectionStrategy, Component, DestroyRef, inject, OnInit} from '@angular/core';
import {TokenService} from '../../services/token.service';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {TuiButton, tuiDialog, TuiDialogService, TuiHint} from '@taiga-ui/core';
import {AsyncPipe} from '@angular/common';
import {TuiCardLarge} from '@taiga-ui/layout';
import {FormatDatePipe} from '../../pipes/format-date-pipe';
import {TUI_CONFIRM, TuiBadge, TuiConfirmData} from '@taiga-ui/kit';
import {DateFromNowPipe} from '../../pipes/date-from-now-pipe';
import {DateIsFuturePipe} from '../../pipes/date-is-future-pipe';
import {DateIsSoonPipe} from '../../pipes/date-is-soon-pipe';
import {NewTokenDialog} from '../../components/new-token-dialog/new-token-dialog';
import {TuiLet} from '@taiga-ui/cdk';
import {PermissionInfoPipe} from '../../pipes/permission-info-pipe';
import {NEVER, switchMap} from 'rxjs';
import {TokenEntity} from '../../entities/token-entity';
import moment from 'moment';

@Component({
  selector: 'app-tokens.page',
  imports: [
    TuiButton,
    AsyncPipe,
    TuiCardLarge,
    FormatDatePipe,
    TuiBadge,
    TuiHint,
    DateFromNowPipe,
    DateIsFuturePipe,
    DateIsSoonPipe,
    TuiLet,
    PermissionInfoPipe
  ],
  templateUrl: './tokens.page.html',
  styleUrl: './tokens.page.scss',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TokensPage implements OnInit {
  private readonly tokenService = inject(TokenService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly dialogService = inject(TuiDialogService);

  protected readonly tokens$ = this.tokenService.tokens$;

  ngOnInit() {
    this.tokenService.loadTokensOnApplicationChange$.pipe(
      takeUntilDestroyed(this.destroyRef),
    ).subscribe();
  }

  private readonly newTokenDialog = tuiDialog(NewTokenDialog, {
    dismissible: false,
    label: 'Новый токен',
  });

  protected newToken() {
    this.newTokenDialog(undefined).subscribe();
  }

  protected deleteToken(token: TokenEntity): void {
    if (token.expiresAt && token.expiresAt > moment()) {
      const data: TuiConfirmData = {
        content: 'Вы уверены, что хотите отозвать токен?',
        yes: 'Да',
        no: 'Нет',
      };
      this.dialogService
        .open<boolean>(TUI_CONFIRM, {
          label: 'Удаление токена',
          size: 's',
          data,
        })
        .pipe(
          switchMap(result => {
            if (result) {
              return this.tokenService.deleteToken(token.id);
            }
            return NEVER;
          }),
        )
        .subscribe();
    }
    else {
      this.tokenService.deleteToken(token.id).subscribe();
    }
  }
}
