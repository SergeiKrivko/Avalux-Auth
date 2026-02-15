import { Pipe, PipeTransform } from '@angular/core';
import {Moment} from 'moment';
import 'moment/locale/ru';

@Pipe({
  name: 'dateFromNow',
  standalone: true
})
export class DateFromNowPipe implements PipeTransform {

  transform(value: Moment | undefined): string | undefined {
    if (!value)
      return undefined;
    return value.locale('ru').fromNow();
  }

}
