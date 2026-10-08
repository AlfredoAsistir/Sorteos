import { Directive, ElementRef, HostListener, Input, OnDestroy, OnInit } from '@angular/core';
import { FormControl } from '@angular/forms';
import { Subscription } from 'rxjs';

@Directive({ selector: 'input[appDateMask]', standalone: true })
export class DateMaskDirective implements OnInit, OnDestroy {
  @Input({ required: true }) appDateMask!: FormControl<Date | null>;
  private subscription?: Subscription;
  constructor(private readonly element: ElementRef<HTMLInputElement>) {}

  ngOnInit(): void {
    this.subscription = this.appDateMask.valueChanges.subscribe(value => queueMicrotask(() => this.render(value)));
    this.render(this.appDateMask.value);
  }
  ngOnDestroy(): void { this.subscription?.unsubscribe(); }

  @HostListener('input', ['$event']) onInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const digits = input.value.replace(/\D/g, '').slice(0, 8);
    const masked = [digits.slice(0, 2), digits.slice(2, 4), digits.slice(4, 8)].filter(Boolean).join('/');
    input.value = masked;
    const match = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(masked);
    let date: Date | null = null;
    if (match) {
      const day = Number(match[1]); const month = Number(match[2]); const year = Number(match[3]);
      const candidate = new Date(year, month - 1, day);
      if (candidate.getFullYear() === year && candidate.getMonth() === month - 1 && candidate.getDate() === day) date = candidate;
    }
    this.appDateMask.setValue(date, { emitEvent: false, emitModelToViewChange: false });
    this.appDateMask.markAsDirty();
  }

  @HostListener('blur') onBlur(): void { this.appDateMask.markAsTouched(); this.render(this.appDateMask.value); }

  private render(date: Date | null): void {
    this.element.nativeElement.value = date
      ? `${String(date.getDate()).padStart(2, '0')}/${String(date.getMonth() + 1).padStart(2, '0')}/${date.getFullYear()}`
      : '';
  }
}
