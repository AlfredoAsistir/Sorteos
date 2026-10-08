import { DOCUMENT } from '@angular/common';
import { computed, DestroyRef, inject, Injectable, signal } from '@angular/core';
import { NotificationService } from '../notifications/notification.service';
import { AppInstallationState, getInstallationState, isChromeOnAndroid, isInstalledMobileApp } from './app-installation.utils';

interface BeforeInstallPromptEvent extends Event {
  prompt(): Promise<void>;
  readonly userChoice: Promise<{ outcome: 'accepted' | 'dismissed'; platform: string }>;
}

interface RelatedApp {
  platform: string;
  url?: string;
  id?: string;
}

type InstallationNavigator = Navigator & {
  getInstalledRelatedApps?: () => Promise<RelatedApp[]>;
};
type InstallationPhase = 'idle' | 'prompting' | 'verifying' | 'slow' | 'unconfirmed' | 'accepted' | 'finishing';

@Injectable({ providedIn: 'root' })
export class AppInstallationService {
  private readonly document = inject(DOCUMENT);
  private readonly notifications = inject(NotificationService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly stateSignal = signal<AppInstallationState>('unsupported');
  private readonly phase = signal<InstallationPhase>('idle');
  private readonly runningInApp = signal(false);
  private readonly browserReportedInstallation = signal(false);
  private deferredPrompt: BeforeInstallPromptEvent | null = null;
  private initialized = false;
  private completionTimer: number | null = null;
  private completionMessageShown = false;
  private activeInstallation = false;
  private readonly installationStorageKey = 'sorteos-gb-installation-registered';
  private destroyed = false;
  private verificationStarted = 0;
  private pollTimer: number | null = null;
  private deadlineTimer: number | null = null;
  private pendingCheck: Promise<boolean | null> | null = null;
  private verificationRun = 0;

  readonly state = this.stateSignal.asReadonly();
  readonly busy = computed(() => ['prompting', 'verifying', 'slow', 'finishing'].includes(this.phase()));
  readonly available = computed(() => !this.runningInApp() && this.stateSignal() !== 'unsupported');
  readonly installationKnown = computed(() => this.stateSignal() === 'installed' || this.browserReportedInstallation());
  readonly showAction = computed(() => !this.installationKnown());
  readonly highlightMessage = computed(() => this.installationKnown() && !this.busy());
  readonly highlightInstall = computed(() => this.stateSignal() === 'android-ready' && !this.busy() && this.showAction());
  readonly title = computed(() => {
    if (this.phase() === 'finishing') return 'Finalizando instalación…';
    if (this.stateSignal() === 'installed') return '¡Tu aplicación ya está lista!';
    return this.browserReportedInstallation() ? 'App Instalada' : 'Lleva Sorteos GB en tu celular';
  });
  readonly actionLabel = computed(() => {
    if (this.phase() === 'prompting') return 'Esperando tu respuesta…';
    if (this.busy()) return 'Verificando instalación…';
    if (['unconfirmed', 'accepted'].includes(this.phase())) return 'Comprobar nuevamente';
    return this.stateSignal() === 'android-ready' ? 'Instalar aplicación' : 'Ver instrucciones';
  });
  readonly description = computed(() => {
    if (this.phase() === 'finishing') return 'Espera unos momentos. Mantén esta página abierta.';
    if (this.stateSignal() === 'installed') return 'Busca Sorteos GB en las aplicaciones de tu celular y úsala como app para una mejor experiencia.';
    switch (this.phase()) {
      case 'prompting': return 'Confirma la instalación en la ventana del navegador.';
      case 'verifying': return 'Mantén esta página abierta mientras verificamos la instalación.';
      case 'slow': return 'La instalación está tardando más de lo esperado. Mantén esta página abierta mientras verificamos.';
      case 'unconfirmed': return 'No pudimos confirmar la instalación. Revisa si Sorteos GB aparece en las aplicaciones de tu celular.';
      case 'accepted': return this.browserReportedInstallation()
        ? 'Tu App Sorteos GB ya se encuentra instalada en tu dispositivo, entra a tus aplicaciones y accede a ella para una mejor experiencia de uso.'
        : 'Solicitud de instalación aceptada. Este navegador no permite confirmarla; revisa las aplicaciones de tu celular.';
      default: return this.stateSignal() === 'ios-other-browser'
        ? 'Abre Sorteos GB en Safari para instalarla.'
        : 'Instala Sorteos GB App';
    }
  });

  initialize(): void {
    if (this.initialized) return;
    this.initialized = true;
    const appWindow = this.document.defaultView;
    if (!appWindow) return;

    if (this.readInstallationRegistration()) {
      this.browserReportedInstallation.set(true);
      this.phase.set('accepted');
    }
    const beforeInstallPrompt = (event: Event) => {
      event.preventDefault();
      this.deferredPrompt = event as BeforeInstallPromptEvent;
      // A fresh offer is stronger evidence than a saved installation history.
      if (!this.busy()) {
        this.browserReportedInstallation.set(false);
        this.saveInstallationRegistration(false);
        this.stateSignal.set('android-ready');
        this.phase.set('idle');
      }
      this.refreshState();
    };
    const appInstalled = () => {
      this.deferredPrompt = null;
      // This browser event is useful even when the related-apps query returns [].
      // It may precede WebAPK completion, so keep it distinct from confirmed installation.
      this.activeInstallation = true;
      this.browserReportedInstallation.set(true);
      this.saveInstallationRegistration(true);
      if (this.stateSignal() === 'installed') return;
      this.scheduleCompletionMessage();
      if (this.pollTimer === null) this.beginVerification();
      else void this.checkInstallation();

    };
    const resume = () => {
      if (this.document.visibilityState === 'hidden') return;
      this.refreshState();
      if (this.pollTimer !== null) void this.pollInstallation(this.verificationRun);
      else void this.checkInstallation();
    };
    appWindow.addEventListener('beforeinstallprompt', beforeInstallPrompt);
    appWindow.addEventListener('appinstalled', appInstalled);
    appWindow.addEventListener('focus', resume);
    appWindow.addEventListener('pageshow', resume);
    this.document.addEventListener('visibilitychange', resume);
    this.destroyRef.onDestroy(() => {
      this.destroyed = true;
      if (this.completionTimer !== null) appWindow.clearTimeout(this.completionTimer);
      this.stopVerification();
      appWindow.removeEventListener('beforeinstallprompt', beforeInstallPrompt);
      appWindow.removeEventListener('appinstalled', appInstalled);
      appWindow.removeEventListener('focus', resume);
      appWindow.removeEventListener('pageshow', resume);
      this.document.removeEventListener('visibilitychange', resume);
    });
    this.refreshState();
    void this.checkInstallation();
  }

  async requestInstallation(): Promise<void> {
    if (this.busy() || this.stateSignal() === 'installed') return;
    if (['unconfirmed', 'accepted'].includes(this.phase())) {
      this.beginVerification();
      return;
    }
    const state = this.stateSignal();
    if (state === 'android-ready' && this.deferredPrompt) {
      const prompt = this.deferredPrompt;
      this.deferredPrompt = null;
      this.activeInstallation = true;
      this.completionMessageShown = false;
      this.phase.set('prompting');
      try {
        await prompt.prompt();
        const choice = await prompt.userChoice;
        if (this.destroyed) return;
        if (this.stateSignal() === 'installed') {
          if (this.completionTimer === null) this.phase.set('idle');
        } else if (choice.outcome === 'accepted') {
          this.beginVerification();
        } else {
          this.phase.set('idle');
          this.refreshState();
        }
      } catch {
        if (this.destroyed) return;
        this.phase.set('idle');
        this.refreshState();
        this.notifications.show('No fue posible iniciar la instalación', 'Inténtalo desde el menú de Google Chrome.', 'error');
      }
      return;
    }
    if (state === 'android-manual') this.notifications.showInstallInstructions('android');
    if (state === 'ios-safari') this.notifications.showInstallInstructions('ios-safari');
    if (state === 'ios-other-browser') this.notifications.showInstallInstructions('ios-other-browser');
  }

  private supportsInstallationCheck(): boolean {
    const appNavigator = this.document.defaultView?.navigator as InstallationNavigator | undefined;
    return !!appNavigator && isChromeOnAndroid(appNavigator) && typeof appNavigator.getInstalledRelatedApps === 'function';
  }

  private beginVerification(): void {
    if (this.pollTimer !== null) return;
    this.stopVerification();
    this.refreshState();
    if (this.stateSignal() === 'installed') return;
    if (!this.supportsInstallationCheck()) {
      if (this.completionTimer === null) this.phase.set('accepted');
      if (!this.browserReportedInstallation()) this.notifications.show('Solicitud de instalación aceptada', this.description(), 'info');
      return;
    }
    const appWindow = this.document.defaultView!;
    this.verificationStarted = Date.now();
    if (this.completionTimer === null) this.phase.set(this.browserReportedInstallation() ? 'accepted' : 'verifying');
    const run = this.verificationRun;
    this.pollTimer = appWindow.setInterval(() => void this.pollInstallation(run), 3000);
    this.deadlineTimer = appWindow.setTimeout(() => void this.finishVerification(run), 60000);
    void this.pollInstallation(run);
  }

  private async pollInstallation(run: number): Promise<void> {
    if (run !== this.verificationRun || this.document.visibilityState === 'hidden') return;
    if (this.completionTimer === null && !this.browserReportedInstallation() && Date.now() - this.verificationStarted >= 30000) this.phase.set('slow');
    await this.checkInstallation();
  }

  private async finishVerification(run: number): Promise<void> {
    if (run !== this.verificationRun) return;
    if (this.document.visibilityState !== 'hidden') await this.checkInstallation();
    if (run !== this.verificationRun || this.destroyed) return;
    this.stopVerification();
    if (this.browserReportedInstallation()) {
      this.phase.set('accepted');
    } else {
      this.phase.set('unconfirmed');
      this.notifications.show('Instalación sin confirmar', this.description(), 'warning');
    }
  }

  private async checkInstallation(): Promise<void> {
    this.refreshState();
    if (this.destroyed || this.runningInApp() || !this.supportsInstallationCheck()) return;
    const appWindow = this.document.defaultView!;
    const appNavigator = appWindow.navigator as InstallationNavigator;
    if (!this.pendingCheck) {
      const manifestLink = this.document.querySelector<HTMLLinkElement>('link[rel="manifest"]');
      if (!manifestLink) return;
      const manifestUrl = new URL(manifestLink.href, this.document.baseURI).href;
      this.pendingCheck = Promise.resolve().then(() => appNavigator.getInstalledRelatedApps!())
        .then(apps => apps.some(app => {
          if (app.platform !== 'webapp') return false;
          // Our manifest declares id "/". Some browsers expose identity without url.
          const appId = new URL('/', manifestUrl).href;
          return (!!app.url && new URL(app.url, manifestUrl).href === manifestUrl) ||
            (!!app.id && new URL(app.id, manifestUrl).href === appId);
        }))
        .catch(() => null)
        .finally(() => { this.pendingCheck = null; });
    }
    // Keep the underlying request shared if the browser never settles it.
    let timeout: number | undefined;
    const installed = await Promise.race([
      this.pendingCheck,
      new Promise<null>(resolve => { timeout = appWindow.setTimeout(() => resolve(null), 2000); })
    ]);
    appWindow.clearTimeout(timeout);
    if (this.destroyed) return;
    if (installed) this.confirmInstalled();
    else if (installed === false && this.completionTimer === null && this.stateSignal() === 'installed') {
      this.stateSignal.set(getInstallationState(appWindow, appWindow.navigator, this.deferredPrompt !== null));
    }
  }

  private confirmInstalled(): void {
    if (this.stateSignal() === 'installed') return;
    this.stopVerification();
    this.deferredPrompt = null;
    this.stateSignal.set('installed');
    this.saveInstallationRegistration(true);
    if (this.activeInstallation && !this.completionMessageShown) this.scheduleCompletionMessage(true);
    else this.phase.set('idle');
  }

  private scheduleCompletionMessage(confirmed = false): void {
    if (this.completionMessageShown) return;
    if (this.completionTimer !== null) {
      if (!confirmed) return;
      this.document.defaultView!.clearTimeout(this.completionTimer);
    }
    this.phase.set('finishing');
    this.completionTimer = this.document.defaultView!.setTimeout(() => {
      this.completionTimer = null;
      if (this.destroyed) return;
      this.completionMessageShown = true;
      this.activeInstallation = false;
      if (this.stateSignal() === 'installed') {
        this.phase.set('idle');
        this.notifications.show('Aplicación instalada',
          'Sorteos GB está instalada. Ya puedes encontrarla en las aplicaciones de tu celular.', 'success');
      } else {
        this.phase.set('accepted');
        this.notifications.show('Instalación registrada', this.description(), 'info');
      }
    }, 5000);
  }

  private readInstallationRegistration(): boolean {
    try {
      return this.document.defaultView?.localStorage.getItem(this.installationStorageKey) === 'registered';
    } catch {
      return false;
    }
  }

  private saveInstallationRegistration(registered: boolean): void {
    try {
      const storage = this.document.defaultView?.localStorage;
      if (registered) storage?.setItem(this.installationStorageKey, 'registered');
      else storage?.removeItem(this.installationStorageKey);
    } catch {
      // Storage is optional; browser detection still works when it is blocked.
    }
  }

  private stopVerification(): void {
    const appWindow = this.document.defaultView;
    if (this.pollTimer !== null) appWindow?.clearInterval(this.pollTimer);
    if (this.deadlineTimer !== null) appWindow?.clearTimeout(this.deadlineTimer);
    this.pollTimer = null;
    this.deadlineTimer = null;
    this.verificationRun++;
  }

  private refreshState(): void {
    const appWindow = this.document.defaultView;
    if (!appWindow) return;
    const runningInApp = isInstalledMobileApp(appWindow, appWindow.navigator);
    this.runningInApp.set(runningInApp);
    if (runningInApp) this.confirmInstalled();
    else if (this.stateSignal() !== 'installed') this.stateSignal.set(getInstallationState(
      appWindow, appWindow.navigator, this.deferredPrompt !== null
    ));
  }
}





