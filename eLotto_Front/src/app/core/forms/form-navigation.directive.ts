import { Directive, ElementRef, HostListener } from '@angular/core';

@Directive({ selector: 'form', standalone: true })
export class FormNavigationDirective {
  constructor(private readonly element: ElementRef<HTMLFormElement>) {}
  @HostListener('keydown', ['$event']) onKeydown(event: KeyboardEvent): void {
    if (event.key !== 'Enter' && event.key !== 'Escape') return;
    const target = event.target as HTMLElement;
    if (target.closest('button')) return;
    event.preventDefault();
    const controls = Array.from(this.element.nativeElement.querySelectorAll<HTMLElement>(
      'input:not([disabled]),textarea:not([disabled]),button[role="switch"]:not([disabled])'
    )).filter(control => control.tabIndex >= 0 && control.getClientRects().length > 0);
    const index = controls.indexOf(target);
    const next = controls[index + (event.key === 'Enter' ? 1 : -1)];
    next?.focus();
  }
}
