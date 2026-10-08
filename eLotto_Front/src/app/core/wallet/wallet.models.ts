export interface Wallet {
  balance: number;
  currency: string;
  name?: string;
  email?: string;
  minimumDepositAmount: number;
  maximumDepositAmount: number;
  suggestedDepositAmount: number;
  stripePublishableKey: string;
}

export type DepositPaymentMethod = 'card' | 'oxxo' | 'bank_transfer';

export interface PaymentIntentResult {
  clientSecret: string;
  transactionId: number;
  instructionUrl?: string;
  whatsAppSent?: boolean;
}

export interface DepositStatusResult {
  transactionId: number;
  status: number;
  stripeMessage?: string;
  userMessage?: string;
}

export interface WalletTransaction {
  id: number;
  type: number;
  amount: number;
  status: number;
  description: string;
  stripeMessage?: string;
  userMessage?: string;
  createdAt: string;
  completedAt?: string;
}

export interface PagedWalletTransactions {
  items: WalletTransaction[];
  page: number;
  pageSize: number;
  totalCount: number;
}
