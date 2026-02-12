import {Component, DestroyRef, inject, OnInit} from '@angular/core';
import {Router, RouterOutlet} from '@angular/router';
import {TuiRoot} from '@taiga-ui/core';
import {AuthService} from './services/auth.service';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {combineLatest, from, NEVER, of, switchMap} from 'rxjs';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, TuiRoot],
  templateUrl: './app.html',
  standalone: true,
  styleUrl: './app.scss'
})
export class App implements OnInit {
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly authService = inject(AuthService);

  ngOnInit() {
    combineLatest([
      this.authService.load(),
      this.authService.state$.pipe(
        switchMap(state => {
          if (state.isLoaded && !state.isAuthenticated)
            return from(this.router.navigate(['/login'])).pipe(switchMap(() => NEVER));
          return NEVER;
        }),
      )
    ]).pipe(
      takeUntilDestroyed(this.destroyRef),
    ).subscribe();
  }
}
