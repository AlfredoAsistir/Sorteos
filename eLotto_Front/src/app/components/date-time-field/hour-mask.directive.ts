import { Directive, ElementRef, HostListener, Input, OnDestroy, OnInit } from '@angular/core';
import { FormControl } from '@angular/forms';
import { Subscription } from 'rxjs';

@Directive({ selector: 'input[appHourMask]', standalone: true })
export class HourMaskDirective implements OnInit, OnDestroy {
  @Input({ required: true }) appHourMask!: FormControl<string>;
  private subscription?: Subscription;
  constructor(private readonly element: ElementRef<HTMLInputElement>) {}

  ngOnInit(): void {
    this.subscription = this.appHourMask.valueChanges.subscribe(value => queueMicrotask(() => this.render(value)));
    this.render(this.appHourMask.value);
  }
  ngOnDestroy(): void { this.subscription?.unsubscribe(); }

  @HostListener('input', ['$event']) onInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const digits = input.value.replace(/\D/g, '').slice(0, 2);
    input.value = digits ? `${digits}:00` : '';
    const hour = Number(digits);
    const value = digits.length === 2 && hour >= 0 && hour <= 23 ? `${digits}:00` : '';
    this.appHourMask.setValue(value, { emitEvent: false, emitModelToViewChange: false });
    this.appHourMask.markAsDirty();
  }

  @HostListener('blur') onBlur(): void { this.appHourMask.markAsTouched(); this.render(this.appHourMask.value); }
  private render(value: string): void { this.element.nativeElement.value = /^(?:[01]\d|2[0-3]):00$/.test(value) ? value : ''; }
}
