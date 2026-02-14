import {inject, Pipe, PipeTransform} from '@angular/core';
import {ProviderService} from '../services/provider.service';
import {Observable} from 'rxjs';
import {ProviderInfoEntity} from '../entities/provider-entity';

@Pipe({
  name: 'providerInfo',
  standalone: true
})
export class ProviderInfoPipe implements PipeTransform {
  private readonly providerService = inject(ProviderService);

  transform(value: number): Observable<ProviderInfoEntity | undefined> {
    return this.providerService.providerInfoById(value);
  }

}
