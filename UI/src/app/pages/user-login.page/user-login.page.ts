import {Component, inject} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from '@angular/forms';
import {TuiButton, TuiError, TuiLabel, TuiTextfieldComponent, TuiTextfieldDirective} from '@taiga-ui/core';
import {TuiCardLarge} from '@taiga-ui/layout';
import {TuiSegmented} from '@taiga-ui/kit';
import {AsyncPipe} from '@angular/common';
import {ApiClient, PasswordSignInSchema, PasswordSignUpSchema} from '../../services/api-client';
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

  protected error = new BehaviorSubject<string>("");

  protected readonly clientName$ = this.apiClient.clientInfo(this.route.snapshot.queryParams['state']).pipe(
    tap(console.log),
    map(resp => resp.name),
  );

  protected submit() {
    const state = this.route.snapshot.queryParams['state'];

    (this.control.value.isSignUp
      ? this.apiClient.signup(state, PasswordSignUpSchema.fromJS({
        login: this.control.value.login,
        password: this.control.value.password,
        userInfo: {
          id: this.control.value.login,
          name: this.control.value.username,
          login: this.control.value.login,
          email: this.control.value.email,
        }
      }))
      : this.apiClient.signin(state, PasswordSignInSchema.fromJS({
        login: this.control.value.login,
        password: this.control.value.password,
      })))
      .pipe(
        catchError((error: Error) => {
          this.error.next(error.message);
          return NEVER;
        }),
        tap(resp => {
          console.log(resp);
          // if (resp.redirectUrl)
          //   window.location.href = resp.redirectUrl;
        }),
      ).subscribe();
  }
}

export default UserLoginPage
