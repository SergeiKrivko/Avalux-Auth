import { Pipe, PipeTransform } from '@angular/core';
import {ProviderInfoEntity} from '../entities/provider-entity';

@Pipe({
  name: 'providerRedirectUrl',
  standalone: true
})
export class ProviderRedirectUrlPipe implements PipeTransform {

  transform(value: ProviderInfoEntity): unknown {
    return `${window.location.protocol}//${window.location.host}/api/v1/auth/${value.key}/callback`;
  }

}
