import {ChangeDetectionStrategy, Component, DestroyRef, inject, OnInit} from '@angular/core';
import {TuiInputRange, TuiPagination} from '@taiga-ui/kit';
import {UserService} from '../../services/user.service';
import {AsyncPipe} from '@angular/common';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {UserCard} from '../../components/user-card/user-card';

@Component({
  selector: 'app-users.page',
  imports: [
    TuiPagination,
    AsyncPipe,
    TuiInputRange,
    UserCard
  ],
  templateUrl: './users.page.html',
  styleUrl: './users.page.scss',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UsersPage implements OnInit {
  private readonly userService = inject(UserService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly users$ = this.userService.users$;
  protected readonly page$ = this.userService.page$;
  protected readonly totalPages$ = this.userService.totalPages$;

  ngOnInit() {
    this.userService.loadUsers$.pipe(
      takeUntilDestroyed(this.destroyRef),
    ).subscribe();
  }

  goToPage(index: number) {
    console.log(index);
    this.userService.selectPage(index);
  }
}
