import { Pipe, PipeTransform } from '@angular/core';
import {Moment} from 'moment';

@Pipe({
  name: 'formatDate',
  standalone: true
})
export class FormatDatePipe implements PipeTransform {

  transform(value: Moment | undefined): string | undefined {
    if (!value)
      return undefined;
    return value.format('DD.MM.YYYY');
  }

}
