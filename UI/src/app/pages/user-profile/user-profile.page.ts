import {ChangeDetectorRef, Component, inject, OnInit} from '@angular/core';
import {TuiCardLarge} from '@taiga-ui/layout';
import {FormControl, FormGroup, ReactiveFormsModule} from '@angular/forms';
import {TuiButton, TuiError, TuiTextfield} from '@taiga-ui/core';
import {FileUploader} from '../../components/file-uploader/file-uploader';
import {TuiAvatar, TuiSwitch} from '@taiga-ui/kit';
import {
  ApiClient,
  ApiException,
  UpdateProfileSchema
} from '../../services/api-client';
import {ActivatedRoute} from '@angular/router';
import {BehaviorSubject, catchError, map, NEVER, tap} from 'rxjs';
import {AsyncPipe} from '@angular/common';

@Component({
  selector: 'app-user-profile',
  imports: [
    TuiCardLarge,
    ReactiveFormsModule,
    TuiTextfield,
    TuiError,
    FileUploader,
    TuiAvatar,
    AsyncPipe,
    TuiButton,
    TuiSwitch
  ],
  templateUrl: './user-profile.page.html',
  styleUrl: './user-profile.page.scss',
})
export class UserProfilePage implements OnInit {
  private readonly apiClient = inject(ApiClient);
  private readonly route = inject(ActivatedRoute);
  private readonly changeDetectorRef = inject(ChangeDetectorRef);

  protected readonly control = new FormGroup({
    changePassword: new FormControl<boolean>(false),
    username: new FormControl<string>(""),
    email: new FormControl<string>(""),
    avatarId: new FormControl<string>(""),
    oldPassword: new FormControl<string>(""),
    newPassword: new FormControl<string>(""),
    newPasswordAgain: new FormControl<string>(""),
  });

  protected error = new BehaviorSubject<string | null>(null);

  protected readonly changePassword$ = this.control.valueChanges.pipe(
    map(e => e.changePassword)
  );
  protected readonly clientName$ = this.apiClient.clientInfo(this.route.snapshot.queryParams['state']).pipe(
    map(resp => resp.name),
  );

  ngOnInit() {
    this.apiClient.profileGET(this.route.snapshot.queryParams['state']).pipe(
      tap(resp => {
        this.control.setValue({
          changePassword: false,
          oldPassword: "",
          newPassword: "",
          newPasswordAgain: "",
          username: resp.name ?? null,
          email: resp.email ?? null,
          avatarId: resp.avatarId ?? null,
        });
        this.changeDetectorRef.detectChanges();
      }),
    ).subscribe();
  }

  protected submit() {
    const state = this.route.snapshot.queryParams['state'];
    const value = this.control.value;
    if (value.changePassword && (!value.newPassword || value.newPassword?.length < 8)) {
      this.error.next("Слишком короткий пароль");
      return;
    }
    if (value.changePassword && value.newPassword != value.newPasswordAgain) {
      this.error.next("Пароли не совпадают");
      return;
    }

    this.apiClient.profilePUT(state, UpdateProfileSchema.fromJS({
      oldPassword: value.changePassword ? value.oldPassword || undefined : undefined,
      newPassword: value.changePassword ? value.newPassword || undefined : undefined,
      userInfo: {
        name: value.username || null,
        email: value.email || null,
        avatarId: value.avatarId || null,
      }
    })).pipe(
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
