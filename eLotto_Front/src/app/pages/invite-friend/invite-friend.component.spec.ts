import { ChangeDetectorRef } from '@angular/core';
import { of } from 'rxjs';
import { NotificationService } from '../../core/notifications/notification.service';
import { LoadingService } from '../../services/loading.service';
import { ReferralInvitationService } from '../../services/referral-invitation.service';
import { InviteFriendComponent } from './invite-friend.component';

describe('InviteFriendComponent', () => {
  function createComponent() {
    const service = jasmine.createSpyObj<ReferralInvitationService>(
      'ReferralInvitationService',
      ['get']
    );
    const notifications = jasmine.createSpyObj<NotificationService>(
      'NotificationService',
      ['show']
    );
    const writeText = jasmine.createSpy('writeText').and.resolveTo();
    const open = jasmine.createSpy('open');
    const document = {
      location: { origin: 'https://sorteos.example' },
      defaultView: {
        navigator: { clipboard: { writeText } },
        open,
      },
    } as unknown as Document;

    const component = new InviteFriendComponent(
      service,
      notifications,
      jasmine.createSpyObj<LoadingService>('LoadingService', ['show', 'hide']),
      jasmine.createSpyObj<ChangeDetectorRef>('ChangeDetectorRef', ['markForCheck']),
      document
    );

    return { component, service, notifications, writeText, open };
  }

  it('builds the approved registration link with the own referral code', () => {
    const { component, service } = createComponent();
    service.get.and.returnValue(of({ referralCode: 'ABC12345' }));

    component.ngOnInit();

    expect(component.referralCode).toBe('ABC12345');
    expect(component.invitationLink)
      .toBe('https://sorteos.example/authentication/side-register?ref=ABC12345');
    expect(component.invitationMessage).toBe(
      '¡Hola! Te invito a registrarte en Sorteos Global Broker. Usa mi enlace de invitación para crear tu cuenta:'
    );
  });

  it('copies the editable message and referral link as one invitation', async () => {
    const { component, service, notifications, writeText } = createComponent();
    service.get.and.returnValue(of({ referralCode: 'ABC12345' }));
    component.ngOnInit();
    component.invitationMessage = 'Mi mensaje personalizado';

    await component.copyLink();

    expect(writeText).toHaveBeenCalledWith(
      'Mi mensaje personalizado\n\nhttps://sorteos.example/authentication/side-register?ref=ABC12345'
    );
    expect(notifications.show).toHaveBeenCalledWith(
      'Invitación copiada',
      'La invitación se copió al portapapeles.',
      'success'
    );
  });

  it('opens WhatsApp with the same invitation including special characters and line breaks', () => {
    const { component, service, open } = createComponent();
    service.get.and.returnValue(of({ referralCode: 'ABC12345' }));
    component.ngOnInit();
    component.invitationMessage = '¡Hola & bienvenida!\nMensaje personalizado.';

    component.openWhatsApp();

    expect(open).toHaveBeenCalledTimes(1);
    const [url, target, features] = open.calls.mostRecent().args;
    expect(url).toContain('https://wa.me/?text=');
    expect(decodeURIComponent(url)).toBe(
      'https://wa.me/?text=¡Hola & bienvenida!\nMensaje personalizado.\n\n' +
      'https://sorteos.example/authentication/side-register?ref=ABC12345'
    );
    expect(target).toBe('_blank');
    expect(features).toBe('noopener,noreferrer');
  });

  it('copies only the generated link when the editable message is empty', async () => {
    const { component, service, writeText } = createComponent();
    service.get.and.returnValue(of({ referralCode: 'ABC12345' }));
    component.ngOnInit();
    component.invitationMessage = '   \n ';

    await component.copyLink();

    expect(writeText).toHaveBeenCalledWith(
      'https://sorteos.example/authentication/side-register?ref=ABC12345'
    );
  });

  it('editing the message never changes the referral code or generated link', () => {
    const { component, service } = createComponent();
    service.get.and.returnValue(of({ referralCode: 'ABC12345' }));
    component.ngOnInit();
    const originalLink = component.invitationLink;

    component.invitationMessage = 'Otro texto con ?ref=ZZZZZZZZ';

    expect(component.referralCode).toBe('ABC12345');
    expect(component.invitationLink).toBe(originalLink);
    expect(component.buildInvitationText()).toContain('?ref=ABC12345');
  });
});
