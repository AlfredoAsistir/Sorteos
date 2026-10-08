import { DOCUMENT } from '@angular/common';
import { Component, ChangeDetectionStrategy, DestroyRef, inject, NgZone } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { MatDialog } from '@angular/material/dialog';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize, fromEvent, interval, merge, Observable } from 'rxjs';
import { LoadingOverlayComponent } from './components/loading-overlay/loading-overlay.component';
import { APP_BRANDING } from './config/branding.config';
import { SessionService } from './core/auth/session.service';
import { NotificationService } from './core/notifications/notification.service';
import { AppInstallationService } from './core/pwa/app-installation.service';
import { AppUpdateService } from './core/pwa/app-update.service';
import { AuthService } from './services/auth.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, LoadingOverlayComponent],
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './app.component.html',
})
export class AppComponent {
  private readonly document = inject(DOCUMENT);
  private readonly session = inject(SessionService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly notifications = inject(NotificationService);
  private readonly dialog = inject(MatDialog);
  private readonly zone = inject(NgZone);
  private readonly destroyRef = inject(DestroyRef);
  private readonly appInstallation = inject(AppInstallationService);
  private readonly appUpdate = inject(AppUpdateService);
  private checkingSession = false;
  readonly title = APP_BRANDING.name;

  constructor(documentTitle: Title) {
    this.appInstallation.initialize();
    this.appUpdate.initialize();
    documentTitle.setTitle(this.title);
    this.document
      .querySelector<HTMLLinkElement>('link[rel~="icon"]')
      ?.setAttribute('href', APP_BRANDING.faviconUrl);

    this.session.sessionEnded$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(reason => this.zone.run(() => {
        this.dialog.closeAll();
        void this.router.navigate(['/authentication/login'], { replaceUrl: true }).then(() => {
          if(reason === 'replaced'){
          const message = 'Se inició sesión en otro dispositivo.';
          this.notifications.show('Sesión finalizada', message, 'warning');
          }
        });
      }));

    const checks: Observable<unknown>[] = [
      interval(30_000),
      fromEvent(this.document, 'visibilitychange'),
    ];
    if (this.document.defaultView) {
      checks.push(fromEvent(this.document.defaultView, 'focus'));
    }
    merge(...checks)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.checkActiveSession());
    void this.session.whenReady().then(() => this.checkActiveSession());
  }

  private checkActiveSession(): void {
    if (this.checkingSession || this.document.visibilityState === 'hidden') return;

    const hadSession = !!this.session.token;
    if (!this.session.isAuthenticated()) {
      if (hadSession) {
        this.dialog.closeAll();
        void this.router.navigate(['/authentication/login'], { replaceUrl: true });
      }
      return;
    }

    this.checkingSession = true;
    this.auth.checkSession()
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => { this.checkingSession = false; })
      )
      .subscribe({ error: () => undefined });
  }
}
