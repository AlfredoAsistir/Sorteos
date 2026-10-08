import { environment } from '../../../environments/environment';
import { paymentInstructionApiUrl, paymentInstructionApiUrlFromPublicUrl } from './payment-instruction-url';

describe('paymentInstructionApiUrl', () => {
  it('dirige la referencia pública al endpoint de instrucciones del API', () => {
    expect(paymentInstructionApiUrl('AbC123')).toBe(`${environment.apiUrl}/p/AbC123`);
    expect(paymentInstructionApiUrl('2g-SdYoyi75pI')).toBe(`${environment.apiUrl}/p/2g-SdYoyi75pI`);
    expect(paymentInstructionApiUrl('2g_SdYoyi75pI')).toBe(`${environment.apiUrl}/p/2g_SdYoyi75pI`);
  });

  it('rechaza referencias vacías o con caracteres no permitidos', () => {
    expect(paymentInstructionApiUrl(null)).toBeNull();
    expect(paymentInstructionApiUrl('ABC/123')).toBeNull();
  });
});

describe('paymentInstructionApiUrlFromPublicUrl', () => {
  it('abre el vale en el API sin volver a entrar a la PWA', () => {
    expect(paymentInstructionApiUrlFromPublicUrl('https://sorteos.gbcancun.com/p/2g-SdYoyi75pI'))
      .toBe(`${environment.apiUrl}/p/2g-SdYoyi75pI`);
  });

  it('rechaza rutas ajenas a las instrucciones', () => {
    expect(paymentInstructionApiUrlFromPublicUrl('https://sorteos.gbcancun.com/starter/index')).toBeNull();
    expect(paymentInstructionApiUrlFromPublicUrl('https://sorteos.gbcancun.com/p/ABC/123')).toBeNull();
  });
});
