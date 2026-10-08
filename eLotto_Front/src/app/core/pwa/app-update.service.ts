import { DOCUMENT } from '@angular/common';
import { DestroyRef, Injectable, inject } from '@angular/core';
import { NotificationService } from '../notifications/notification.service';
import {
  AppVersionInfo,
  getEmbeddedAppVersion,
  isAppUpdateAvailable,
  normalizeAppVersion
} from './app-update.utils';

const UPDATE_CHECK_INTERVAL_MS = 15 * 60 * 1000;
const MINIMUM_CHECK_INTERVAL_MS = 10 * 1000;

@Injectable({ providedIn: 'root' })
export class AppUpdateService {
  private readonly document = inject(DOCUMENT);
  private readonly destroyRef = inject(DestroyRef);
  private readonly notifications = inject(NotificationService);
  private readonly currentVersion = getEmbeddedAppVersion(this.document);
  private initialized = false;
  private checking = false;
  private lastCheckAt = 0;
  private dismissedVersion: string | null = null;

  initialize(): void {
    const appWindow = this.document.defaultView;
    if (this.initialized || !appWindow || !this.currentVersion) return;

    this.initialized = true;
    const checkWhenVisible = () => {
      if (this.document.visibilityState !== 'hidden') void this.checkForUpdate();
    };
    const intervalId = appWindow.setInterval(checkWhenVisible, UPDATE_CHECK_INTERVAL_MS);

    this.document.addEventListener('visibilitychange', checkWhenVisible);
    appWindow.addEventListener('pageshow', checkWhenVisible);
    appWindow.addEventListener('focus', checkWhenVisible);
    this.destroyRef.onDestroy(() => {
      appWindow.clearInterval(intervalId);
      this.document.removeEventListener('visibilitychange', checkWhenVisible);
      appWindow.removeEventListener('pageshow', checkWhenVisible);
      appWindow.removeEventListener('focus', checkWhenVisible);
    });

    void this.checkForUpdate(true);
  }

  async checkForUpdate(force = false): Promise<void> {
    const appWindow = this.document.defaultView;
    const now = Date.now();
    if (!appWindow || !this.currentVersion || this.checking ||
        (!force && now - this.lastCheckAt < MINIMUM_CHECK_INTERVAL_MS)) return;

    this.checking = true;
    this.lastCheckAt = now;
    try {
      const response = await appWindow.fetch(
        new URL('version.json', this.document.baseURI),
        { cache: 'no-store', headers: { Accept: 'application/json' } }
      );
      if (!response.ok) return;

      const info = await response.json() as AppVersionInfo;
      const latestVersion = normalizeAppVersion(info.version);
      if (!isAppUpdateAvailable(this.currentVersion, latestVersion) ||
          latestVersion === this.dismissedVersion) return;

      const accepted = await this.notifications.confirm(
        'Actualización disponible',
        'Hay una nueva versión de Sorteos GB disponible. Actualiza para obtener las mejoras más recientes.',
        'Actualizar ahora',
        'Más tarde'
      );

      if (accepted) appWindow.location.reload();
      else this.dismissedVersion = latestVersion;
    } catch {
      // La comprobación es auxiliar y no debe interrumpir el uso de la aplicación.
    } finally {
      this.checking = false;
    }
  }
}
