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
export class App {
}
