import { CommonModule } from '@angular/common';
import { ServerLocalDatePipe } from '../../core/date/server-local-date.pipe';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectorRef, Component, OnDestroy, OnInit, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { MaterialModule } from '../../material.module';
import { NotificationService } from '../../core/notifications/notification.service';
import { CurrentUserTickets, UserTicketPurchase } from '../../core/user-lottery/user-lottery.models';
import { LoadingService } from '../../services/loading.service';
import { UserLotteryService } from '../../services/user-lottery.service';

@Component({
  selector: 'app-mis-boletos',
  standalone: true,
  imports: [CommonModule, ServerLocalDatePipe, MaterialModule],
  templateUrl: './mis-boletos.component.html',
  styleUrl: './mis-boletos.component.scss'
})
export class MisBoletosComponent implements OnInit, OnDestroy {
  private readonly service = inject(UserLotteryService);
  private readonly loadingService = inject(LoadingService);
  private readonly notifications = inject(NotificationService);
  private readonly changeDetector = inject(ChangeDetectorRef);
  private cooldownTimer?: ReturnType<typeof setInterval>;
  private cooldownEndsAt = 0;

  tickets: CurrentUserTickets | null = null;
  loaded = false;
  noCurrentLottery = false;
  sendingFolio: string | null = null;
  cooldownRemaining = 0;

  get totalTickets(): number {
    return this.tickets?.compras.reduce((total, purchase) => total + purchase.cantidadBoletos, 0) ?? 0;
  }

  get totalAmount(): number {
    return this.tickets?.compras.reduce((total, purchase) => total + purchase.importe, 0) ?? 0;
  }

  get whatsAppBlocked(): boolean {
    return this.sendingFolio !== null || this.cooldownRemaining > 0;
  }

  async ngOnInit(): Promise<void> {
    this.loadingService.show();
    try {
      this.tickets = await firstValueFrom(this.service.getMyTickets());
      this.startCooldown(this.tickets.reenvioDisponibleEnSegundos);
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 404) {
        this.noCurrentLottery = true;
      } else {
        this.notifications.show('No fue posible cargar tus boletos', this.errorMessage(error), 'error');
      }
    } finally {
      this.loaded = true;
      this.loadingService.hide();
      this.changeDetector.detectChanges();
    }
  }

  ngOnDestroy(): void {
    if (this.cooldownTimer) clearInterval(this.cooldownTimer);
    this.loadingService.hide();
  }

  async resendByWhatsApp(purchase: UserTicketPurchase): Promise<void> {
    if (this.whatsAppBlocked) return;

    this.sendingFolio = purchase.folioCompra;
    this.loadingService.show();
    try {
      const result = await firstValueFrom(
        this.service.resendTicketsByWhatsApp(purchase.folioCompra)
      );
      this.startCooldown(result.reintentarEnSegundos);
      this.notifications.show('Comprobante enviado', result.mensaje, 'success');
    } catch (error) {
      const retryAfter = this.retryAfterSeconds(error);
      if (retryAfter > 0) {
        this.startCooldown(retryAfter);
        this.notifications.show('Espera antes de reenviar', this.errorMessage(error), 'info');
      } else {
        this.notifications.show('No fue posible enviar el comprobante', this.errorMessage(error), 'error');
      }
    } finally {
      this.sendingFolio = null;
      this.loadingService.hide();
      this.changeDetector.detectChanges();
    }
  }

  resendButtonLabel(folio: string): string {
    if (this.sendingFolio === folio) return 'Enviando…';
    if (this.sendingFolio) return 'Envío en proceso';
    if (this.cooldownRemaining > 0) return `Disponible en ${this.cooldownRemaining} s`;
    return 'Enviar por WhatsApp';
  }

  private startCooldown(seconds: number): void {
    if (this.cooldownTimer) clearInterval(this.cooldownTimer);
    this.cooldownTimer = undefined;
    this.cooldownRemaining = Math.max(0, Math.ceil(seconds));
    if (this.cooldownRemaining === 0) return;

    this.cooldownEndsAt = performance.now() + this.cooldownRemaining * 1000;
    this.cooldownTimer = setInterval(() => {
      this.cooldownRemaining = Math.max(
        0,
        Math.ceil((this.cooldownEndsAt - performance.now()) / 1000)
      );
      this.changeDetector.detectChanges();
      if (this.cooldownRemaining === 0 && this.cooldownTimer) {
        clearInterval(this.cooldownTimer);
        this.cooldownTimer = undefined;
      }
    }, 250);
  }

  private retryAfterSeconds(error: unknown): number {
    if (!(error instanceof HttpErrorResponse)) return 0;
    const response = error.error as { reintentarEnSegundos?: number } | null;
    return Math.max(0, Number(response?.reintentarEnSegundos ?? 0));
  }

  private errorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      const response = error.error as { mensaje?: string; message?: string } | null;
      return response?.mensaje ?? response?.message ?? 'No fue posible completar la operación.';
    }
    return error instanceof Error ? error.message : 'No fue posible completar la operación.';
  }
}
