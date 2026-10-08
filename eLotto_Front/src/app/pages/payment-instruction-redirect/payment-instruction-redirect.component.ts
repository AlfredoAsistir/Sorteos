import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { paymentInstructionApiUrl } from './payment-instruction-url';

@Component({
  selector: 'app-payment-instruction-redirect',
  template: '<p>Abriendo tus instrucciones de depósito…</p>',
  styles: [`
    :host {
      min-height: 100vh;
      display: grid;
      place-items: center;
      padding: 24px;
      text-align: center;
    }
  `]
})
export class PaymentInstructionRedirectComponent implements OnInit {
  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router
  ) {}

  ngOnInit(): void {
    const destination = paymentInstructionApiUrl(this.route.snapshot.paramMap.get('code'));
    if (!destination) {
      void this.router.navigate(['/authentication/error']);
      return;
    }

    window.location.replace(destination);
  }
}
