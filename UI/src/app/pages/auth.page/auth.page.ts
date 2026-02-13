import {ChangeDetectionStrategy, Component, inject} from '@angular/core';
import {AuthService} from '../../services/auth.service';
import {AsyncPipe} from '@angular/common';
import {TuiCard} from '@taiga-ui/layout';
import {TuiAppearance, TuiButton, TuiTextfield} from '@taiga-ui/core';
import {Logo} from '../../components/logo/logo';
import {FormControl, FormGroup, ReactiveFormsModule} from '@angular/forms';
import {from, switchMap} from 'rxjs';
import {Router} from '@angular/router';

@Component({
  selector: 'app-auth.page',
  imports: [
    TuiCard,
    TuiButton,
    Logo,
    ReactiveFormsModule,
    TuiTextfield,
    TuiAppearance
  ],
  templateUrl: './auth.page.html',
  styleUrl: './auth.page.scss',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuthPage {
  private readonly authService: AuthService = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly control = new FormGroup({
    login: new FormControl<string>(""),
    password: new FormControl<string>(""),
  })

  protected authorize() {
    this.authService.logIn(this.control.value.login ?? "", this.control.value.password ?? "").pipe(
      switchMap(() => from(this.router.navigate(['/']))),
    ).subscribe();
  }
}
