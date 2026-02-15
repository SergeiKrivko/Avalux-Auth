import { Pipe, PipeTransform } from '@angular/core';
import moment, {Moment} from 'moment';

@Pipe({
  name: 'dateIsSoon',
  standalone: true
})
export class DateIsSoonPipe implements PipeTransform {

  transform(value: Moment | undefined): boolean {
    if (!value)
      return false;
    return value.diff(moment(), 'days') < 7;
  }

}
