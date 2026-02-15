import {inject, Pipe, PipeTransform} from '@angular/core';
import {TokenService} from '../services/token.service';
import {TokenPermissionEntity} from '../entities/token-entity';
import {map, Observable, of} from 'rxjs';

@Pipe({
  name: 'permissionInfo',
  standalone: true
})
export class PermissionInfoPipe implements PipeTransform {
  private readonly tokenService = inject(TokenService);

  transform(value: string | undefined): Observable<TokenPermissionEntity | undefined> {
    if (!value)
      return of(undefined);
    return this.tokenService.permissions$.pipe(
      map(permissions => permissions.find(e => e.key === value)),
    );
  }

}
