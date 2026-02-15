import { Pipe, PipeTransform } from '@angular/core';
import moment, {Moment} from 'moment';

@Pipe({
  name: 'dateIsFuture',
  standalone: true
})
export class DateIsFuturePipe implements PipeTransform {

  transform(value: Moment | undefined): boolean {
    if (!value)
      return false;
    return value > moment();
  }

}
