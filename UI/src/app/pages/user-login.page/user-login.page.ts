import {Component, inject} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from '@angular/forms';
import {TuiButton, TuiError, TuiLabel, TuiTextfieldComponent, TuiTextfieldDirective} from '@taiga-ui/core';
import {TuiCardLarge} from '@taiga-ui/layout';
import {TuiSegmented} from '@taiga-ui/kit';
import {AsyncPipe} from '@angular/common';
import {ApiClient, ApiException, PasswordSignInSchema, PasswordSignUpSchema} from '../../services/api-client';
import {ActivatedRoute} from '@angular/router';
import {BehaviorSubject, catchError, map, NEVER, tap} from 'rxjs';

@Component({
  selector: 'app-user-login.page',
  imports: [
    ReactiveFormsModule,
    TuiButton,
    TuiCardLarge,
    TuiLabel,
    TuiTextfieldComponent,
    TuiTextfieldDirective,
    TuiSegmented,
    AsyncPipe,
    TuiError
  ],
  templateUrl: './user-login.page.html',
  styleUrl: './user-login.page.scss',
})
class UserLoginPage {
  private readonly apiClient = inject(ApiClient);
  private readonly route = inject(ActivatedRoute);

  protected readonly control = new FormGroup({
    isSignUp: new FormControl<boolean>(false),
    login: new FormControl<string>("", Validators.required),
    password: new FormControl<string>("", Validators.required),
    passwordAgain: new FormControl<string>(""),
    username: new FormControl<string>(""),
    email: new FormControl<string>(""),
  });

  protected readonly isSignUp$ = this.control.get('isSignUp')?.valueChanges;

  protected error = new BehaviorSubject<string | null>(null);

  protected readonly clientName$ = this.apiClient.clientInfo(this.route.snapshot.queryParams['state']).pipe(
    map(resp => resp.name),
  );

  protected submit() {
    const state = this.route.snapshot.queryParams['state'];
    const value = this.control.value;
    if (value.isSignUp && (!value.password || value.password?.length < 8)) {
      this.error.next("Слишком короткий пароль");
      return;
    }
    if (value.isSignUp && value.password != value.passwordAgain) {
      this.error.next("Пароли не совпадают");
      return;
    }

    (value.isSignUp
      ? this.apiClient.signup(state, PasswordSignUpSchema.fromJS({
        login: value.login,
        password: value.password,
        userInfo: {
          id: value.login,
          name: value.username,
          login: value.login,
          email: value.email,
        }
      }))
      : this.apiClient.signin(state, PasswordSignInSchema.fromJS({
        login: value.login,
        password: value.password,
      })))
      .pipe(
        catchError((error: ApiException) => {
          this.error.next(error.response);
          return NEVER;
        }),
        catchError((error: Error) => {
          this.error.next(error.message);
          return NEVER;
        }),
        tap(resp => {
          if (resp.redirectUrl)
            window.location.href = resp.redirectUrl;
        }),
      ).subscribe();
  }
}

export default UserLoginPage
