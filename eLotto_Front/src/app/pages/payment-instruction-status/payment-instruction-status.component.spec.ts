import { paymentInstructionStatusContent } from './payment-instruction-status.component';

describe('paymentInstructionStatusContent', () => {
  it('explica de forma amigable que el vale fue usado o expiró', () => {
    const content = paymentInstructionStatusContent('no-disponible');

    expect(content.title).toBe('Vale no disponible');
    expect(content.message).toContain('utilizado o expiró');
  });

  it('distingue un error temporal de un vale no disponible', () => {
    const content = paymentInstructionStatusContent('error');

    expect(content.title).toBe('No pudimos abrir el vale');
    expect(content.detail).toContain('nuevamente');
  });
});
