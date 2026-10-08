import { ChangeDetectorRef, Component, Input, Optional, Self } from '@angular/core';
import { ControlValueAccessor, NgControl } from '@angular/forms';
import { MaterialModule } from '../../material.module';

@Component({
  selector: 'app-numeric-input',
  standalone: true,
  imports: [MaterialModule],
  templateUrl: './numeric-input.component.html',
  styleUrl: './numeric-input.component.scss'
})
export class NumericInputComponent implements ControlValueAccessor {
  private static nextId = 0;
  @Input({ required: true }) label = '';
  @Input() labelAbove = false;
  readonly inputId = `numeric-input-${++NumericInputComponent.nextId}`;
  @Input() integerDigits = 9;
  @Input() decimalDigits = 0;
  value = ''; disabled = false;
  private change: (value: number | null) => void = () => {};
  private touched: () => void = () => {};
  constructor(@Optional() @Self() public readonly ngControl: NgControl | null, private readonly changeDetector: ChangeDetectorRef) { if (ngControl) ngControl.valueAccessor = this; }
  writeValue(value: number | null): void { this.value = value == null ? '' : String(value); this.changeDetector.markForCheck(); }
  registerOnChange(fn: (value: number | null) => void): void { this.change = fn; }
  registerOnTouched(fn: () => void): void { this.touched = fn; }
  setDisabledState(disabled: boolean): void { this.disabled = disabled; this.changeDetector.markForCheck(); }
  input(event: Event): void {
    const element = event.target as HTMLInputElement;
    const parts = element.value.replace(/[^\d.]/g, '').split('.');
    this.value = parts[0].slice(0, this.integerDigits) + (this.decimalDigits && parts.length > 1 ? `.${parts.slice(1).join('').slice(0, this.decimalDigits)}` : '');
    element.value = this.value;
    this.change(this.value === '' ? null : Number(this.value));
  }
  blur(): void { this.touched(); }
}
