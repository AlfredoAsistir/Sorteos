import { Component, Input } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MAT_DATE_LOCALE, provideNativeDateAdapter } from '@angular/material/core';
import { MaterialModule } from '../../material.module';
import { DateMaskDirective } from './date-mask.directive';
import { HourMaskDirective } from './hour-mask.directive';

@Component({ selector: 'app-date-time-field', standalone: true, imports: [ReactiveFormsModule, MaterialModule, DateMaskDirective, HourMaskDirective], providers: [provideNativeDateAdapter(), { provide: MAT_DATE_LOCALE, useValue: 'es-MX' }], templateUrl: './date-time-field.component.html', styleUrl: './date-time-field.component.scss' })
export class DateTimeFieldComponent {
  private static nextId = 0;
  readonly hours = Array.from({ length: 24 }, (_, hour) => `${String(hour).padStart(2, '0')}:00`);
  @Input({ required: true }) label = '';
  @Input() labelAbove = false;
  readonly fieldId = ++DateTimeFieldComponent.nextId;
  @Input({ required: true }) dateControl!: FormControl<Date | null>;
  @Input({ required: true }) timeControl!: FormControl<string>;
  selectHour(hour: string): void { this.timeControl.setValue(hour); this.timeControl.markAsDirty(); this.timeControl.markAsTouched(); }
}
