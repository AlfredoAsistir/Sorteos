import { CommonModule } from '@angular/common';
import { ServerLocalDatePipe } from '../../core/date/server-local-date.pipe';
import { AfterViewInit, ApplicationRef, Component, ElementRef, OnDestroy, OnInit, ViewChild, inject, ChangeDetectionStrategy, NgZone } from '@angular/core';
import { AbstractControl, FormControl, ReactiveFormsModule, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';
import { Stripe, StripeElements, StripePaymentElement, loadStripe } from '@stripe/stripe-js';
import { firstValueFrom } from 'rxjs';
import { TablerIconsModule } from '@luoxiao123/angular-tabler-icons';
import { MaterialModule } from '../../material.module';
import { DepositPaymentMethod, WalletTransaction } from '../../core/wallet/wallet.models';
import { WalletService } from '../../services/wallet.service';
import { LoadingService } from '../../services/loading.service';
import { UserLotteryService } from '../../services/user-lottery.service';
import { NotificationService } from '../../core/notifications/notification.service';
import { APP_OPERATIONAL_CONFIG } from '../../config/operational.config';
import { RouterModule } from '@angular/router';
import { paymentInstructionApiUrlFromPublicUrl } from '../payment-instruction-redirect/payment-instruction-url';

export const completeNameValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value = String(control.value ?? '').trim();
  if (!value) return null;

  const parts = value.split(/\s+/);
  const validPart = /^\p{L}+(?:[.'’\-]\p{L}+)*$/u;
  const meaningfulParts = parts.filter(part => part.replace(/[.'’\-]/g, '').length >= 2);

  if (parts.some(part => !validPart.test(part))) return { invalidNameCharacters: true };
  return parts.length >= 2 && meaningfulParts.length >= 2 ? null : { incompleteName: true };
};

@Component({
  selector: 'app-wallet',
  standalone: true,
  imports: [CommonModule, ServerLocalDatePipe, ReactiveFormsModule, RouterModule, MaterialModule, TablerIconsModule],
  templateUrl: './wallet.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./wallet.component.scss'],
})
export class WalletComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('quickAmounts', { read: ElementRef }) private quickAmounts?: ElementRef<HTMLElement>;
  readonly walletService = inject(WalletService);
  private readonly loadingService = inject(LoadingService);
  private readonly userLotteryService = inject(UserLotteryService);
  private readonly notifications = inject(NotificationService);
  private readonly ngZone = inject(NgZone);
  private readonly applicationRef = inject(ApplicationRef);
  private publishableKey = '';
  private stripe: Stripe | null = null;
  private elements: StripeElements | null = null;
  private paymentElement: StripePaymentElement | null = null;
  private pollingTimer?: ReturnType<typeof setTimeout>;
  private depositBlockTimer?: ReturnType<typeof setTimeout>;
  private currentTransactionId?: number;
  private quickAmountsVisibilityObserver?: IntersectionObserver;

  readonly amount = new FormControl<number>(APP_OPERATIONAL_CONFIG.wallet.fallbackSuggestedDepositAmount, {
    nonNullable: true,
    validators: [Validators.required, Validators.min(APP_OPERATIONAL_CONFIG.wallet.fallbackMinimumDepositAmount), Validators.max(APP_OPERATIONAL_CONFIG.wallet.fallbackMaximumDepositAmount)],
  });
  balance = 0;
  currency = 'MXN';
  minimumDepositAmount: number = APP_OPERATIONAL_CONFIG.wallet.fallbackMinimumDepositAmount;
  maximumDepositAmount: number = APP_OPERATIONAL_CONFIG.wallet.fallbackMaximumDepositAmount;
  readonly quickDepositAmounts = APP_OPERATIONAL_CONFIG.wallet.quickDepositAmounts;
  readonly billingName = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.maxLength(150), completeNameValidator],
  });
  readonly billingEmail = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.email, Validators.maxLength(254)],
  });
  transactions: WalletTransaction[] = [];
  processing = false;
  depositCompleted = false;
  selectedPaymentMethod: DepositPaymentMethod = 'bank_transfer';
  cardReady = false;
  paymentEntryMode: 'unknown' | 'saved' | 'new' = 'unknown';
  message = '';
  instructionHint = '';
  instructionUrl = '';
  instructionViewUrl = '';
  instructionLabel = '';
  error = '';
  showMobileDepositSummary = false;
  depositBlocked = false;
  depositBlockedMessage = '';
  private currentSorteoId: number | null = null;

  async ngOnInit(): Promise<void> {
    await Promise.all([this.refreshWallet(), this.refreshDepositAvailability()]);
  }

  ngAfterViewInit(): void {
    if (typeof document === 'undefined' || typeof IntersectionObserver === 'undefined' || !this.quickAmounts) return;

    const headerHeight = document.querySelector<HTMLElement>('.topbar')?.getBoundingClientRect().height ?? 0;
    this.quickAmountsVisibilityObserver = new IntersectionObserver(([entry]) => {
      const passedHeader = !entry.isIntersecting && entry.boundingClientRect.bottom <= headerHeight;
      this.ngZone.run(() => (this.showMobileDepositSummary = passedHeader));
    }, { rootMargin: `-${headerHeight}px 0px 0px`, threshold: 0 });
    this.quickAmountsVisibilityObserver.observe(this.quickAmounts.nativeElement);
  }
  ngOnDestroy(): void {
    this.quickAmountsVisibilityObserver?.disconnect();
    if (this.pollingTimer) clearTimeout(this.pollingTimer);
    if (this.depositBlockTimer) clearTimeout(this.depositBlockTimer);
    this.paymentElement?.destroy();
    this.loadingService.hide();
  }

  selectAmount(value: number): void {
    if (!this.processing && !this.depositBlocked) this.amount.setValue(value);
  }

  get actionLabel(): string {
    if (this.selectedPaymentMethod === 'oxxo') return 'Generar vale OXXO';
    if (this.selectedPaymentMethod === 'bank_transfer') return 'Obtener datos SPEI';
    if (!this.cardReady) return 'Continuar';
    return 'Depositar';
  }

  get selectedPaymentMethodLabel(): string {
    if (this.selectedPaymentMethod === 'oxxo') return 'OXXO';
    if (this.selectedPaymentMethod === 'bank_transfer') return 'Transferencia SPEI';
    return 'Tarjeta';
  }
  selectPaymentMethod(method: DepositPaymentMethod): void {
    if (!this.processing && !this.cardReady && !this.depositCompleted && !this.depositBlocked) this.selectedPaymentMethod = method;
  }

  async deposit(): Promise<void> {
    if (this.depositBlocked || this.currentSorteoId === null) {
      const message = this.depositBlockedMessage ||
        'No puedes agregar saldo porque actualmente no hay un sorteo disponible.';
      this.notifications.show('Depósitos no disponibles', message, 'info');
      return;
    }
    if (this.processing || this.depositCompleted || this.amount.invalid) return;
    if (this.selectedPaymentMethod !== 'card' && (this.billingName.invalid || this.billingEmail.invalid)) {
      this.billingName.markAsTouched();
      this.billingEmail.markAsTouched();
      return;
    }

    this.processing = true;
    this.loadingService.show();
    this.error = '';
    this.message = '';
    this.instructionHint = '';
    this.instructionUrl = '';
    this.instructionViewUrl = '';
    this.instructionLabel = '';

    try {
      if (this.selectedPaymentMethod !== 'card') {
        const result = await firstValueFrom(this.walletService.createDepositPaymentIntent(
          this.currentSorteoId,
          this.amount.value,
          this.selectedPaymentMethod,
          { name: this.billingName.value.trim().replace(/\s+/g, ' '), email: this.billingEmail.value.trim() }
        ));
        this.currentTransactionId = result.transactionId;

        if (!result.instructionUrl) {
          throw new Error('Stripe confirmó el depósito, pero no devolvió las instrucciones.');
        }

        this.instructionUrl = result.instructionUrl;
        this.instructionViewUrl = paymentInstructionApiUrlFromPublicUrl(result.instructionUrl) ?? result.instructionUrl;
        this.instructionLabel = this.selectedPaymentMethod === 'oxxo'
          ? 'Ver e imprimir vale OXXO'
          : 'Ver instrucciones de transferencia SPEI';
        this.message = result.whatsAppSent
          ? `¡Listo! Enviamos ${this.selectedPaymentMethod === 'oxxo' ? 'tus instrucciones para OXXO' : 'tus instrucciones SPEI'} a WhatsApp.`
          : 'Tus instrucciones están listas, aunque no pudimos enviarlas por WhatsApp.';
        this.instructionHint = result.whatsAppSent
          ? 'También puedes consultarlas en la opción de abajo.'
          : 'Consúltalas directamente en la opción de abajo.';
        this.amount.disable({ emitEvent: false });
        this.billingName.disable({ emitEvent: false });
        this.billingEmail.disable({ emitEvent: false });
        this.depositCompleted = true;
        return;
      }

      if (!this.elements) {
        await this.prepareCardPaymentElement();
        return;
      }
      if (!this.stripe) {
        throw new Error('Stripe.js no está disponible.');
      }

      const { error, paymentIntent } = await this.stripe.confirmPayment({
        elements: this.elements,
        redirect: 'if_required',
        confirmParams: { return_url: `${window.location.origin}/starter/wallet` },
      });

      if (error) {
        if (this.currentTransactionId) {
          try {
            await firstValueFrom(this.walletService.syncDepositStatus(this.currentTransactionId));
          } catch {
            // Stripe muestra el rechazo directamente dentro del Payment Element.
          }
        }
        this.resetPaymentState();
        return;
      }
      if (!paymentIntent) {
        throw new Error('Stripe no devolvió el estado del pago.');
      }

      this.message = 'Pago confirmado. Esperando la acreditación segura del webhook…';
      await this.pollForCredit(0);
    } catch (error) {
      this.error = this.errorMessage(error);
    } finally {
      this.processing = false;
      this.ngZone.run(() => {
        this.loadingService.hide();
        setTimeout(() => this.applicationRef.tick(), 0);
      });
    }
  }

  statusLabel(status: number): string {
    return ({ 1: 'Pendiente', 2: 'Completado', 3: 'Fallido', 4: 'Cancelado' } as Record<number, string>)[status] ?? 'Desconocido';
  }

  transactionLabel(type: number, description: string): string {
    return walletTransactionLabel(type, description);
  }

  private async prepareCardPaymentElement(): Promise<void> {
    if (!(this.publishableKey.startsWith('pk_test_') || this.publishableKey.startsWith('pk_live_'))) {
      throw new Error('Configura una Publishable Key válida de Stripe para habilitar depósitos.');
    }

    this.stripe = await loadStripe(this.publishableKey);
    if (!this.stripe) throw new Error('Stripe.js no pudo cargarse.');

    const result = await firstValueFrom(
      this.walletService.createDepositPaymentIntent(this.currentSorteoId!, this.amount.value, 'card')
    );
    this.currentTransactionId = result.transactionId;
    this.amount.disable({ emitEvent: false });
    this.elements = this.stripe.elements({ clientSecret: result.clientSecret, locale: 'es' });
    this.paymentElement = this.elements.create('payment', {
      layout: {
        type: 'accordion',
        defaultCollapsed: false,
        radios: 'always',
        spacedAccordionItems: true,
      },
    });
    this.paymentElement.on('change', event => {
      if (event.value.type === 'link' || event.value.payment_method) {
        this.paymentEntryMode = 'saved';
        return;
      }
      if (event.empty) this.paymentEntryMode = 'new';
    });
    this.paymentElement.mount('#stripe-payment-element');
    this.paymentElement.on('ready', () => (this.cardReady = true));
  }

  private async refreshWallet(): Promise<void> {
    try {
      const [wallet, transactions] = await Promise.all([
        firstValueFrom(this.walletService.getWallet()),
        firstValueFrom(this.walletService.getTransactions()),
      ]);
      this.balance = wallet.balance;
      this.publishableKey = wallet.stripePublishableKey ?? '';
      this.minimumDepositAmount = wallet.minimumDepositAmount;
      this.maximumDepositAmount = wallet.maximumDepositAmount;
      this.amount.setValidators([
        Validators.required,
        Validators.min(wallet.minimumDepositAmount),
        Validators.max(wallet.maximumDepositAmount),
      ]);
      if (!this.amount.dirty) this.amount.setValue(wallet.suggestedDepositAmount);
      this.amount.updateValueAndValidity({ emitEvent: false });
      this.currency = wallet.currency;
      if (!this.billingName.dirty) this.billingName.setValue(wallet.name ?? '');
      if (!this.billingEmail.dirty) this.billingEmail.setValue(wallet.email ?? '');
      this.transactions = transactions.items;
    } catch (error) {
      this.error = this.errorMessage(error);
    }
  }

  private async refreshDepositAvailability(): Promise<void> {
    if (this.depositBlockTimer) clearTimeout(this.depositBlockTimer);
    try {
      const lottery = await firstValueFrom(this.userLotteryService.getCurrent());
      this.currentSorteoId = lottery.id;
      if (!lottery.ventaDisponible) {
        this.blockDeposits(
          lottery.estado === 'proximo_a_iniciar'
            ? 'El sorteo está próximo a iniciar. Los depósitos están temporalmente cerrados.'
            : 'El sorteo está en proceso. Los depósitos están temporalmente cerrados.'
        );
        return;
      }

      const millisecondsUntilBlock = Math.max(0, lottery.segundosParaCierreVentas) * 1000;
      this.depositBlockTimer = setTimeout(() => {
        this.ngZone.run(() => this.blockDeposits(
          'El sorteo está próximo a iniciar. Los depósitos están temporalmente cerrados.'
        ));
      }, Math.min(millisecondsUntilBlock, 2_147_000_000));
    } catch {
      this.currentSorteoId = null;
      this.blockDeposits(
        'No puedes agregar saldo porque actualmente no hay un sorteo disponible.'
      );
    }
  }

  private blockDeposits(message: string): void {
    if (this.depositBlocked) return;
    this.depositBlocked = true;
    this.depositBlockedMessage = message;
    this.notifications.show('Depósitos no disponibles', message, 'info');
  }

  private async pollForCredit(attempt: number): Promise<void> {
    await this.refreshWallet();
    const latest = this.transactions.find(transaction => transaction.id === this.currentTransactionId);
    if (latest?.status === 2) {
      this.message = 'Depósito acreditado correctamente.';
      this.processing = false;
      this.depositCompleted = true;
      return;
    }
    if (latest?.status === 3 || latest?.status === 4) {
      this.error = `El depósito terminó con estado: ${this.statusLabel(latest.status)}.`;
      this.processing = false;
      return;
    }
    if (attempt >= APP_OPERATIONAL_CONFIG.wallet.paymentStatusMaximumAttempts) {
      this.message = 'El pago fue recibido y la acreditación sigue pendiente. El saldo se actualizará al procesar el webhook.';
      this.processing = false;
      return;
    }
    await new Promise<void>(resolve => {
      this.pollingTimer = setTimeout(resolve, APP_OPERATIONAL_CONFIG.wallet.paymentStatusPollingIntervalMs);
    });
    await this.pollForCredit(attempt + 1);
  }

  private errorMessage(error: unknown): string {
    if (typeof error === 'object' && error !== null && 'error' in error) {
      const response = (error as { error?: { message?: string } }).error;
      if (response?.message) return response.message;
    }
    return error instanceof Error ? error.message : 'No fue posible completar la operación.';
  }

  private resetPaymentState(): void {
    this.depositCompleted = false;
    this.message = '';
    this.instructionHint = '';
    this.error = '';
    this.processing = false;
  }


}

export function walletTransactionLabel(type: number, description: string): string {
  if (type === 5) return 'Premio de Rascadito';
  if (type === 6) return description || 'Bono por depósito referido';
  return description;
}

