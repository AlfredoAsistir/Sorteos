import { Component } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

export interface PaymentInstructionStatusContent {
  title: string;
  message: string;
  detail: string;
}

export function paymentInstructionStatusContent(status: string | null): PaymentInstructionStatusContent {
  if (status === 'error') {
    return {
      title: 'No pudimos abrir el vale',
      message: 'Ocurrió un problema temporal al consultar las instrucciones.',
      detail: 'Inténtalo nuevamente en unos minutos.'
    };
  }

  return {
    title: 'Vale no disponible',
    message: 'Este vale ya fue utilizado o expiró.',
    detail: 'Si realizaste el pago, tu saldo se acreditará automáticamente cuando sea confirmado.'
  };
}

@Component({
  selector: 'app-payment-instruction-status',
  imports: [RouterLink],
  templateUrl: './payment-instruction-status.component.html',
  styleUrl: './payment-instruction-status.component.scss'
})
export class PaymentInstructionStatusComponent {
  readonly content: PaymentInstructionStatusContent;

  constructor(route: ActivatedRoute) {
    this.content = paymentInstructionStatusContent(route.snapshot.paramMap.get('status'));
  }
}
