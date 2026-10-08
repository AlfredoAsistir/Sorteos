import { ActivatedRoute, convertToParamMap, Router } from '@angular/router';
import { of } from 'rxjs';

import { NotificationService } from '../../../core/notifications/notification.service';
import { RegisterRequest } from '../../../core/auth/auth.models';
import { AuthService } from '../../../services/auth.service';
import { CoreService } from '../../../services/core.service';
import { LoadingService } from '../../../services/loading.service';
import { AppSideRegisterComponent } from './side-register.component';

describe('AppSideRegisterComponent referral registration', () => {
  function createComponent(referralCode?: string): {
    component: AppSideRegisterComponent;
    auth: jasmine.SpyObj<AuthService>;
    notifications: jasmine.SpyObj<NotificationService>;
  } {
    const router = jasmine.createSpyObj<Router>('Router', ['navigate']);
    const auth = jasmine.createSpyObj<AuthService>('AuthService', ['register']);
    const notifications = jasmine.createSpyObj<NotificationService>('NotificationService', ['show']);
    const route = {
      snapshot: {
        queryParamMap: convertToParamMap(
          referralCode === undefined ? {} : { ref: referralCode }
        ),
      },
    } as unknown as ActivatedRoute;

    const component = new AppSideRegisterComponent(
      new CoreService(),
      router,
      route,
      auth,
      notifications,
      new LoadingService()
    );
    component.ngOnInit();

    return { component, auth, notifications };
  }

  function completeForm(component: AppSideRegisterComponent): void {
    component.form.patchValue({
      name: 'Usuario invitado',
      user: 'invitado01',
      whatsapp: '6621234567',
      password: 'Password1!',
      confirmPassword: 'Password1!',
      country: 'MX +52',
      confirmedOver18: true,
    });
  }

  it('requires the over-18 confirmation before sending registration', () => {
    const { component, auth } = createComponent();
    auth.register.and.returnValue(of({ ok: false, message: 'Validación controlada' }));
    completeForm(component);
    component.form.controls.confirmedOver18.setValue(false);

    component.submit();

    expect(component.form.invalid).toBeTrue();
    expect(component.form.controls.confirmedOver18.touched).toBeTrue();
    expect(auth.register).not.toHaveBeenCalled();
  });

  it('sends the explicit over-18 confirmation', () => {
    const { component, auth } = createComponent();
    auth.register.and.returnValue(of({ ok: false, message: 'Validación controlada' }));
    completeForm(component);

    component.submit();

    expect(auth.register.calls.mostRecent().args[0].confirmedOver18).toBeTrue();
  });

  it('normalizes, displays as disabled, and sends the referral query parameter', () => {
    const { component, auth } = createComponent(' abc12345 ');
    auth.register.and.returnValue(of({ ok: false, message: 'Validación controlada' }));
    completeForm(component);

    expect(component.referralCode).toBe('ABC12345');
    expect(component.form.controls.referralCode.disabled).toBeTrue();
    expect(component.form.controls.referralCode.value).toBe('ABC12345');

    component.submit();

    const request = auth.register.calls.mostRecent().args[0];
    expect(request.referralCode).toBe('ABC12345');
  });

  it('keeps normal registration unchanged when ref is absent', () => {
    const { component, auth } = createComponent();
    auth.register.and.returnValue(of({ ok: false, message: 'Validación controlada' }));
    completeForm(component);

    component.submit();

    const request: RegisterRequest = auth.register.calls.mostRecent().args[0];
    expect(component.referralCode).toBeNull();
    expect(request.referralCode).toBeUndefined();
    expect(Object.prototype.hasOwnProperty.call(request, 'referralCode')).toBeFalse();
  });

  it('shows the backend referral validation message', () => {
    const { component, auth, notifications } = createComponent('INVALID-');
    auth.register.and.returnValue(of({
      ok: false,
      message: 'El código de referido no tiene un formato válido.',
    }));
    completeForm(component);

    component.submit();

    expect(notifications.show).toHaveBeenCalledWith(
      'Error al registrarse',
      'El código de referido no tiene un formato válido.',
      'error'
    );
  });
});
