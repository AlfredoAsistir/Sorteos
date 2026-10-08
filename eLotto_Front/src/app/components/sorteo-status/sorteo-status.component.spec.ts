import { CurrentLottery } from '../../core/user-lottery/user-lottery.models';
import { SorteoStatusComponent } from './sorteo-status.component';

describe('SorteoStatusComponent', () => {
  it('prioriza la reprogramación sobre el estado en proceso y un PDF existente', () => {
    const component = new SorteoStatusComponent();
    component.lottery = {
      estado: 'en_proceso',
      ventaDisponible: false,
      cantidadBoletos: 100,
      boletosVendidos: 59,
      porcentajeMinimoVenta: 80,
    } as CurrentLottery;
    component.transparencyState = 'publicado';

    expect(component.rescheduleReason).toBe('reprogramar');

    component.lottery = { ...component.lottery, boletosVendidos: 80 };
    expect(component.rescheduleReason).toBeNull();
  });
});
