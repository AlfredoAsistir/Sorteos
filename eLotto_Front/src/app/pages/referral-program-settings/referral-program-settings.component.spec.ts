import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectorRef } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { of, throwError } from 'rxjs';
import { NotificationService } from '../../core/notifications/notification.service';
import { ReferralProgramSettings } from '../../core/referrals/referral-program-settings.models';
import { LoadingService } from '../../services/loading.service';
import { ReferralProgramSettingsService } from '../../services/referral-program-settings.service';
import { ReferralProgramSettingsComponent } from './referral-program-settings.component';

describe('ReferralProgramSettingsComponent', () => {
  const settings: ReferralProgramSettings = {
    id: 1,
    isActive: true,
    depositRewardPercentage: 10,
    maxRewardedDeposits: 5,
    winnerCashRewardAmount: 5000,
    minimumConfirmedTickets: 100,
    updatedAt: '2026-09-23T12:00:00',
    rowVersion: 'AAAAAAAAAAE=',
  };

  function createComponent(): {
    component: ReferralProgramSettingsComponent;
    service: jasmine.SpyObj<ReferralProgramSettingsService>;
    notifications: jasmine.SpyObj<NotificationService>;
  } {
    const service = jasmine.createSpyObj<ReferralProgramSettingsService>(
      'ReferralProgramSettingsService',
      ['get', 'update']
    );
    const notifications = jasmine.createSpyObj<NotificationService>(
      'NotificationService',
      ['show']
    );
    const component = new ReferralProgramSettingsComponent(
      new FormBuilder(),
      service,
      notifications,
      jasmine.createSpyObj<LoadingService>('LoadingService', ['show', 'hide']),
      jasmine.createSpyObj<ChangeDetectorRef>('ChangeDetectorRef', ['markForCheck'])
    );
    return { component, service, notifications };
  }

  it('loads the singleton and accepts all configured zero values', () => {
    const { component, service } = createComponent();
    service.get.and.returnValue(of({
      ...settings,
      depositRewardPercentage: 0,
      maxRewardedDeposits: 0,
      winnerCashRewardAmount: 0,
      minimumConfirmedTickets: 0,
    }));

    component.ngOnInit();

    expect(component.form.valid).toBeTrue();
    expect(component.form.pristine).toBeTrue();
    expect(component.form.getRawValue()).toEqual({
      isActive: true,
      depositRewardPercentage: 0,
      maxRewardedDeposits: 0,
      winnerCashRewardAmount: 0,
      minimumConfirmedTickets: 0,
    });
  });

  it('sends the current RowVersion and replaces it with the response', () => {
    const { component, service, notifications } = createComponent();
    service.get.and.returnValue(of(settings));
    service.update.and.returnValue(of({
      ...settings,
      isActive: false,
      depositRewardPercentage: 7.5,
      rowVersion: 'AAAAAAAAAAI=',
    }));
    component.ngOnInit();
    component.form.patchValue({ isActive: false, depositRewardPercentage: 7.5 });
    component.form.markAsDirty();

    component.save();

    expect(service.update).toHaveBeenCalledWith({
      isActive: false,
      depositRewardPercentage: 7.5,
      maxRewardedDeposits: 5,
      winnerCashRewardAmount: 5000,
      minimumConfirmedTickets: 100,
      rowVersion: 'AAAAAAAAAAE=',
    });
    expect(component.settings?.rowVersion).toBe('AAAAAAAAAAI=');
    expect(component.form.pristine).toBeTrue();
    expect(notifications.show).toHaveBeenCalledWith(
      'Operación correcta',
      'La configuración del programa de referidos fue actualizada.',
      'success'
    );
  });

  it('warns and reloads after a concurrency conflict', () => {
    const { component, service, notifications } = createComponent();
    const current = { ...settings, depositRewardPercentage: 6, rowVersion: 'AAAAAAAAAAI=' };
    service.get.and.returnValues(of(settings), of(current));
    service.update.and.returnValue(throwError(() => new HttpErrorResponse({ status: 409 })));
    component.ngOnInit();
    component.form.controls.depositRewardPercentage.setValue(8);
    component.form.markAsDirty();

    component.save();

    expect(service.get).toHaveBeenCalledTimes(2);
    expect(component.form.controls.depositRewardPercentage.value).toBe(6);
    expect(component.settings?.rowVersion).toBe('AAAAAAAAAAI=');
    expect(notifications.show).toHaveBeenCalledWith(
      'Configuración desactualizada',
      'Otro administrador modificó esta configuración. Se cargarán los valores vigentes.',
      'warning'
    );
  });
});
