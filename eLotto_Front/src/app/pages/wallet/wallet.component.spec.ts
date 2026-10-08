import { FormControl } from '@angular/forms';
import { completeNameValidator, walletTransactionLabel } from './wallet.component';

describe('Wallet complete name validation', () => {
  it('rejects a single name before requesting a payment from Stripe', () => {
    const control = new FormControl('José', completeNameValidator);

    expect(control.hasError('incompleteName')).toBeTrue();
  });

  it('accepts a name containing at least two valid parts', () => {
    const control = new FormControl('José Alfredo', completeNameValidator);

    expect(control.valid).toBeTrue();
  });

  it('accepts accents, apostrophes and hyphens in a complete name', () => {
    const control = new FormControl("María-José O'Connor", completeNameValidator);

    expect(control.valid).toBeTrue();
  });

  it('rejects numbers and unsupported symbols in the name', () => {
    const control = new FormControl('José Alfredo123', completeNameValidator);

    expect(control.hasError('invalidNameCharacters')).toBeTrue();
  });
});

describe('Wallet transaction labels', () => {
  it('distinguishes both referral deposit reward descriptions', () => {
    expect(walletTransactionLabel(6, 'Bono por depósito referido'))
      .toBe('Bono por depósito referido');
    expect(walletTransactionLabel(6, 'Bono por depósito como referido'))
      .toBe('Bono por depósito como referido');
    expect(walletTransactionLabel(6, ''))
      .toBe('Bono por depósito referido');
  });

  it('keeps existing descriptions for unrelated transaction types', () => {
    expect(walletTransactionLabel(1, 'Depósito con tarjeta'))
      .toBe('Depósito con tarjeta');
  });
});
