import { ChangeDetectorRef } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { NotificationService } from '../../core/notifications/notification.service';
import { LoadingService } from '../../services/loading.service';
import { SorteosService } from '../../services/sorteos.service';
import { SorteosComponent } from './sorteos.component';

describe('SorteosComponent scratchcard defaults', () => {
  it('initializes the requested prize pool and keeps Stripe deposits at thirty pesos', () => {
    const component = new SorteosComponent(
      new FormBuilder(),
      jasmine.createSpyObj<SorteosService>('SorteosService', ['getPage']),
      jasmine.createSpyObj<NotificationService>('NotificationService', ['show', 'confirmDeletion']),
      jasmine.createSpyObj<ChangeDetectorRef>('ChangeDetectorRef', ['detectChanges']),
      jasmine.createSpyObj<LoadingService>('LoadingService', ['show', 'hide'])
    );

    component.new();
    component.form.controls.rascaditosHabilitados.setValue(true);
    component.onScratchcardToggle(true);

    expect(component.form.controls.ganadoresPorGrupo.value).toBe(1);
    expect(component.form.controls.rascaditosPorGrupo.value).toBe(20);
    expect(component.form.controls.importeDepositoStripePorRascadito.value).toBe(30);
    expect(component.form.controls.porcentajeMinimoVenta.value).toBe(70);
    component.form.controls.porcentajeMinimoVenta.setValue(100);
    expect(component.form.controls.porcentajeMinimoVenta.hasError('max')).toBeTrue();
    component.form.controls.porcentajeMinimoVenta.setValue(70);
    expect(component.rascaditoPremios.getRawValue()).toEqual([
      { id: 0, premio: 50, cantidad: 70, entregados: 0 },
      { id: 0, premio: 100, cantidad: 60, entregados: 0 },
      { id: 0, premio: 200, cantidad: 30, entregados: 0 },
      { id: 0, premio: 300, cantidad: 15, entregados: 0 },
    ]);
    expect(component.totalPremiosConfigurados).toBe(175);
    expect(component.bolsaTotal).toBe(20000);
  });
});