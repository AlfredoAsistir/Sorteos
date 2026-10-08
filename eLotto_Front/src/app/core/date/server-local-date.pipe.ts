import { Pipe, PipeTransform } from '@angular/core';

/** Displays the wall-clock components sent by the API without using the viewer's timezone. */
@Pipe({ name: 'serverLocalDate', standalone: true })
export class ServerLocalDatePipe implements PipeTransform {
  transform(value: string | null | undefined, format = 'dd/MM/yyyy, HH:mm'): string {
    if (!value) return '';
    const match = /^(\d{4})-(\d{2})-(\d{2})(?:[T ](\d{2}):(\d{2}))?/.exec(value);
    if (!match) return value;
    const [, year, month, day, hour = '00', minute = '00'] = match;
    if (format === 'dd/MM/yyyy') return `${day}/${month}/${year}`;
    if (format === 'dd/MM/yyyy HH:mm') return `${day}/${month}/${year} ${hour}:${minute}`;
    if (format === 'medium') {
      const months = ['ene.', 'feb.', 'mar.', 'abr.', 'may.', 'jun.', 'jul.', 'ago.', 'sep.', 'oct.', 'nov.', 'dic.'];
      return `${day} ${months[Number(month) - 1] ?? month} ${year}, ${hour}:${minute}`;
    }
    return `${day}/${month}/${year}, ${hour}:${minute}`;
  }
}
