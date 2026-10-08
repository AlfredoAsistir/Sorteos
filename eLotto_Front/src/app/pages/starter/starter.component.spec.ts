import { ChangeDetectorRef } from '@angular/core';
import { fakeAsync, tick } from '@angular/core/testing';
import { FormBuilder } from '@angular/forms';
import { StarterComponent } from './starter.component';

describe('StarterComponent', () => {
  it('impide generar, consultar y comprar boletos aunque la venta siga marcada como disponible', async () => {
    const service = jasmine.createSpyObj('UserLotteryService', ['preReserveRandom', 'queryManual', 'purchase']);
    const notifications = jasmine.createSpyObj('NotificationService', ['show']);
    const component = new StarterComponent(
      new FormBuilder(),
      service,
      jasmine.createSpyObj('WalletService', ['getWallet']),
      jasmine.createSpyObj('LoadingService', ['show', 'hide']),
      { roleName: 'User' } as never,
      notifications,
      jasmine.createSpyObj<ChangeDetectorRef>('ChangeDetectorRef', ['detectChanges'])
    );
    component.lottery = {
      ventaDisponible: true,
      cantidadBoletos: 100,
      boletosVendidos: 100
    } as never;

    await component.generateRandom();
    await component.queryManual();
    await component.purchaseSelectedTickets();

    expect(service.preReserveRandom).not.toHaveBeenCalled();
    expect(service.queryManual).not.toHaveBeenCalled();
    expect(service.purchase).not.toHaveBeenCalled();
    expect(notifications.show).toHaveBeenCalledTimes(3);
    expect(notifications.show).toHaveBeenCalledWith(
      'Boletos agotados',
      'Se vendió el 100% de los boletos de este sorteo.',
      'info'
    );
  });

  it('extiende 30 segundos la lectura y restaura el intervalo tras cambiar la imagen', fakeAsync(() => {
    const service = jasmine.createSpyObj('UserLotteryService', ['imageUrl']);
    service.imageUrl.and.callFake((path: string) => path);
    const component = new StarterComponent(
      new FormBuilder(),
      service,
      jasmine.createSpyObj('WalletService', ['getWallet']),
      jasmine.createSpyObj('LoadingService', ['show', 'hide']),
      { roleName: 'User' } as never,
      jasmine.createSpyObj('NotificationService', ['show']),
      jasmine.createSpyObj<ChangeDetectorRef>('ChangeDetectorRef', ['detectChanges'])
    );
    component.lottery = {
      imagen1: '/primera.webp',
      imagen2: '/segunda.webp',
      imagen3: '/tercera.webp'
    } as never;

    component.nextImage();
    tick(5000);
    component.extendCarouselReadingTime();
    tick(39_999);
    expect(component.carouselIndex).toBe(1);
    tick(1);
    expect(component.carouselIndex).toBe(2);
    tick(9999);
    expect(component.carouselIndex).toBe(2);
    tick(1);
    expect(component.carouselIndex).toBe(0);
    component.ngOnDestroy();
  }));

  it('restaura el intervalo normal al cambiar manualmente de imagen', fakeAsync(() => {
    const service = jasmine.createSpyObj('UserLotteryService', ['imageUrl']);
    service.imageUrl.and.callFake((path: string) => path);
    const component = new StarterComponent(
      new FormBuilder(),
      service,
      jasmine.createSpyObj('WalletService', ['getWallet']),
      jasmine.createSpyObj('LoadingService', ['show', 'hide']),
      { roleName: 'User' } as never,
      jasmine.createSpyObj('NotificationService', ['show']),
      jasmine.createSpyObj<ChangeDetectorRef>('ChangeDetectorRef', ['detectChanges'])
    );
    component.lottery = {
      imagen1: '/primera.webp',
      imagen2: '/segunda.webp',
      imagen3: '/tercera.webp'
    } as never;

    component.nextImage();
    component.extendCarouselReadingTime();
    tick(5000);
    component.selectImage(2);
    tick(9999);
    expect(component.carouselIndex).toBe(2);
    tick(1);
    expect(component.carouselIndex).toBe(0);
    component.ngOnDestroy();
  }));

  it('relaciona el enlace con la imagen visible aunque falte la segunda imagen', () => {
    const service = jasmine.createSpyObj('UserLotteryService', ['imageUrl']);
    service.imageUrl.and.callFake((path: string) => path);
    const component = new StarterComponent(
      new FormBuilder(),
      service,
      jasmine.createSpyObj('WalletService', ['getWallet']),
      jasmine.createSpyObj('LoadingService', ['show', 'hide']),
      { roleName: 'User' } as never,
      jasmine.createSpyObj('NotificationService', ['show']),
      jasmine.createSpyObj<ChangeDetectorRef>('ChangeDetectorRef', ['detectChanges'])
    );

    component.lottery = {
      imagen1: '/principal.webp',
      imagen2: null,
      imagen3: '/referidos.webp',
      imagen3Tema: 'referidos'
    } as never;
    expect(component.images).toEqual(['/principal.webp', '/referidos.webp']);
    expect(component.carouselAction).toBeNull();

    component.carouselIndex = 1;
    expect(component.carouselAction).toEqual({ label: 'Invitar amigos', path: '/starter/invitar-amigo' });

    component.lottery = {
      imagen1: '/principal.webp',
      imagen2: '/rascaditos.webp',
      imagen3: null,
      imagen2Tema: 'rascaditos'
    } as never;
    expect(component.carouselAction).toEqual({
      label: 'Cómo funcionan',
      path: '/starter/preguntas-frecuentes',
      fragment: 'rascaditos'
    });
  });

  it('notifica el límite de 2,000 boletos en Probar suerte', async () => {
    const service = jasmine.createSpyObj('UserLotteryService', ['preReserveRandom']);
    const notifications = jasmine.createSpyObj('NotificationService', ['show']);
    const component = new StarterComponent(
      new FormBuilder(),
      service,
      jasmine.createSpyObj('WalletService', ['getWallet']),
      jasmine.createSpyObj('LoadingService', ['show', 'hide']),
      { roleName: 'User' } as never,
      notifications,
      jasmine.createSpyObj<ChangeDetectorRef>('ChangeDetectorRef', ['detectChanges'])
    );

    component.lottery = { ventaDisponible: true } as never;
    component.randomForm.controls.cantidad.setValue(2001);

    await component.generateRandom();

    expect(notifications.show).toHaveBeenCalledOnceWith(
      'Límite de boletos',
      'Puedes generar un máximo de 2,000 boletos por operación.',
      'warning'
    );
    expect(service.preReserveRandom).not.toHaveBeenCalled();
  });
});
