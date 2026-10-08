import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { map, Observable, Subject, tap } from 'rxjs';
import { environment } from '../../environments/environment';
import { APP_OPERATIONAL_CONFIG } from '../config/operational.config';
import {
  DepositPaymentMethod,
  PagedWalletTransactions,
  DepositStatusResult,
  PaymentIntentResult,
  Wallet,
} from '../core/wallet/wallet.models';

export interface WalletCreditAnimation {
  id: number;
  amount: number;
}

@Injectable({ providedIn: 'root' })
export class WalletService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/api`;
  readonly balance = signal(0);
  readonly currency = signal('MXN');
  readonly walletLoaded = signal(false);
  readonly creditAnimation = signal<WalletCreditAnimation | null>(null);
  private readonly balanceRefreshed = new Subject<void>();
  readonly balanceRefreshed$ = this.balanceRefreshed.asObservable();
  private creditSequence = 0;
  private creditTimer: ReturnType<typeof setTimeout> | null = null;

  getWallet(forceRefresh = false): Observable<Wallet> {
    const options = forceRefresh
      ? {
          headers: new HttpHeaders({
            'Cache-Control': 'no-cache',
            Pragma: 'no-cache',
          }),
          params: new HttpParams().set('_', Date.now().toString()),
        }
      : {};

    return this.http.get<Wallet>(`${this.apiUrl}/wallet`, options).pipe(
      map(response => this.normalizeWallet(response)),
      tap(wallet => {
        this.currency.set(wallet.currency);
        this.setBalanceFromServer(wallet.balance);
      })
    );
  }

  setBalanceFromServer(balance: unknown): void {
    const normalizedBalance = this.toFiniteNumber(balance);
    this.balance.set(normalizedBalance);
    this.walletLoaded.set(true);
    this.balanceRefreshed.next();
  }

  showCredit(amount: unknown): void {
    const credit = this.toFiniteNumber(amount);
    if (credit <= 0) return;
    if (this.creditTimer) clearTimeout(this.creditTimer);
    this.creditAnimation.set(null);
    queueMicrotask(() => {
      this.creditAnimation.set({ id: ++this.creditSequence, amount: credit });
      this.creditTimer = setTimeout(() => {
        this.creditAnimation.set(null);
        this.creditTimer = null;
      }, 4000);
    });
  }

  formatAmount(balance: unknown = this.balance(), currency: string = this.currency()): string {
    const amount = this.toFiniteNumber(balance);
    const currencyCode = currency?.trim().toUpperCase() || 'MXN';

    try {
      return new Intl.NumberFormat('es-MX', {
        style: 'currency',
        currency: currencyCode,
        minimumFractionDigits: 2,
        maximumFractionDigits: 2,
      }).format(amount);
    } catch {
      return `$${amount.toFixed(2)}`;
    }
  }

  getTransactions(page = 1, pageSize = 20): Observable<PagedWalletTransactions> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedWalletTransactions>(`${this.apiUrl}/wallet/transactions`, { params });
  }

  createDepositPaymentIntent(
    sorteoId: number,
    amount: number,
    paymentMethod: DepositPaymentMethod,
    billingDetails?: { name: string; email: string }
  ): Observable<PaymentIntentResult> {
    return this.http.post<PaymentIntentResult>(`${this.apiUrl}/payments/deposits/payment-intent`, {
      sorteoId,
      amount,
      name: billingDetails?.name,
      email: billingDetails?.email,
      paymentMethod,
    });
  }

  syncDepositStatus(transactionId: number): Observable<DepositStatusResult> {
    return this.http.post<DepositStatusResult>(
      `${this.apiUrl}/payments/deposits/${transactionId}/sync`,
      {}
    );
  }

  private normalizeWallet(response: Wallet): Wallet {
    const raw = response as Wallet & {
      Balance?: unknown;
      Currency?: unknown;
      saldo?: unknown;
      Saldo?: unknown;
      MinimumDepositAmount?: unknown;
      MaximumDepositAmount?: unknown;
      SuggestedDepositAmount?: unknown;
      StripePublishableKey?: unknown;
    };
    const balance = raw.balance ?? raw.Balance ?? raw.saldo ?? raw.Saldo ?? 0;
    const currency = raw.currency ?? raw.Currency ?? 'MXN';

    return {
      balance: this.toFiniteNumber(balance),
      currency: typeof currency === 'string' && currency.trim()
        ? currency.trim().toUpperCase()
        : 'MXN',
      name: raw.name,
      email: raw.email,
      minimumDepositAmount: this.toFiniteNumber(raw.minimumDepositAmount ?? raw.MinimumDepositAmount ?? APP_OPERATIONAL_CONFIG.wallet.fallbackMinimumDepositAmount),
      maximumDepositAmount: this.toFiniteNumber(raw.maximumDepositAmount ?? raw.MaximumDepositAmount ?? APP_OPERATIONAL_CONFIG.wallet.fallbackMaximumDepositAmount),
      suggestedDepositAmount: this.toFiniteNumber(raw.suggestedDepositAmount ?? raw.SuggestedDepositAmount ?? APP_OPERATIONAL_CONFIG.wallet.fallbackSuggestedDepositAmount),
      stripePublishableKey: String(raw.stripePublishableKey ?? raw.StripePublishableKey ?? ''),
    };
  }

  private toFiniteNumber(value: unknown): number {
    const amount = typeof value === 'number' ? value : Number(value);
    return Number.isFinite(amount) ? amount : 0;
  }
}



