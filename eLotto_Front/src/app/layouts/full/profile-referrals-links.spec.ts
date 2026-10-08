import { signal, Type } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { OverlayContainer } from '@angular/cdk/overlay';
import { provideRouter } from '@angular/router';
import { TranslateService } from '@ngx-translate/core';
import { TablerIconsModule } from '@luoxiao123/angular-tabler-icons';
import * as TablerIcons from '@luoxiao123/angular-tabler-icons/icons';
import { EMPTY } from 'rxjs';
import { AuthService } from '../../services/auth.service';
import { CoreService } from '../../services/core.service';
import { WalletService } from '../../services/wallet.service';
import { AppHorizontalHeaderComponent } from './horizontal/header/header.component';
import { HeaderComponent as VerticalHeaderComponent } from './vertical/header/header.component';

describe('User referral profile links', () => {
  async function expectLinks(componentType: Type<unknown>): Promise<void> {
    const auth = jasmine.createSpyObj<AuthService>('AuthService', ['logout']);
    Object.defineProperty(auth, 'currentRole', { get: () => 'User' });
    Object.defineProperty(auth, 'currentUser', { get: () => 'Alfredo' });
    const wallet = {
      walletLoaded: signal(false),
      balance: signal(0),
      creditAnimation: signal(null),
      balanceRefreshed$: EMPTY,
      getWallet: () => EMPTY,
      formatAmount: () => '$0.00',
    };

    TestBed.configureTestingModule({
      imports: [componentType, TablerIconsModule.pick(TablerIcons)],
      providers: [
        provideRouter([]),
        CoreService,
        { provide: AuthService, useValue: auth },
        { provide: WalletService, useValue: wallet },
        { provide: MatDialog, useValue: jasmine.createSpyObj('MatDialog', ['open']) },
        {
          provide: TranslateService,
          useValue: jasmine.createSpyObj('TranslateService', ['setDefaultLang', 'use']),
        },
      ],
    });

    const fixture: ComponentFixture<unknown> = TestBed.createComponent(componentType);
    fixture.detectChanges();
    (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('button.profile-menu-trigger')!
      .click();
    fixture.detectChanges();
    await fixture.whenStable();

    const overlay = TestBed.inject(OverlayContainer).getContainerElement();
    const text = overlay.textContent ?? '';
    const invitationIndex = text.indexOf('Invitar a un amigo');
    const referralsIndex = text.indexOf('Mis referidos');
    const logoutIndex = text.indexOf('Cerrar sesión');
    expect(invitationIndex).toBeGreaterThanOrEqual(0);
    expect(referralsIndex).toBeGreaterThan(invitationIndex);
    expect(logoutIndex).toBeGreaterThan(referralsIndex);
    expect(overlay.querySelector('a[href="/starter/mis-referidos"]')).not.toBeNull();

    fixture.destroy();
    overlay.innerHTML = '';
    TestBed.resetTestingModule();
  }

  it('shows Mis referidos in the horizontal User dropdown', async () => {
    await expectLinks(AppHorizontalHeaderComponent);
  });

  it('shows Mis referidos in the vertical User dropdown', async () => {
    await expectLinks(VerticalHeaderComponent);
  });
});
