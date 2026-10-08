import { DOCUMENT } from '@angular/common';
import { fakeAsync, flushMicrotasks, TestBed, tick } from '@angular/core/testing';
import { NotificationService } from '../notifications/notification.service';
import { AppInstallationService } from './app-installation.service';

interface RelatedApp { platform: string; url?: string; id?: string; }

describe('AppInstallationService', () => {
  let service: AppInstallationService;
  let notifications: jasmine.SpyObj<NotificationService>;
  let browser: EventTarget;
  let page: EventTarget;
  let check: jasmine.Spy<() => Promise<RelatedApp[]>>;
  let hidden: boolean;
  let standalone: boolean;
  let storage: Map<string, string>;

  beforeEach(() => {
    hidden = false;
    standalone = false;
    storage = new Map();
    check = jasmine.createSpy('getInstalledRelatedApps').and.resolveTo([]);
    browser = Object.assign(new EventTarget(), {
      navigator: { userAgent: 'Android Chrome/140.0', platform: 'Linux', maxTouchPoints: 1, getInstalledRelatedApps: check },
      matchMedia: () => ({ matches: standalone }),
      localStorage: {
        getItem: (key: string) => storage.get(key) ?? null,
        setItem: (key: string, value: string) => storage.set(key, value),
        removeItem: (key: string) => storage.delete(key)
      },
      setTimeout: window.setTimeout.bind(window), clearTimeout: window.clearTimeout.bind(window),
      setInterval: window.setInterval.bind(window), clearInterval: window.clearInterval.bind(window)
    });
    page = Object.assign(new EventTarget(), {
      defaultView: browser,
      baseURI: 'https://sorteos.example/',
      querySelector: () => ({ href: 'https://sorteos.example/manifest.webmanifest' })
    });
    Object.defineProperty(page, 'visibilityState', { get: () => hidden ? 'hidden' : 'visible' });
    notifications = jasmine.createSpyObj('NotificationService', ['show', 'showInstallInstructions']);
    TestBed.configureTestingModule({ providers: [AppInstallationService,
      { provide: DOCUMENT, useValue: page }, { provide: NotificationService, useValue: notifications }] });
    service = TestBed.inject(AppInstallationService);
  });

  function offerPrompt(outcome: 'accepted' | 'dismissed' = 'accepted'): jasmine.Spy {
    const prompt = jasmine.createSpy('prompt').and.resolveTo();
    const event = Object.assign(new Event('beforeinstallprompt', { cancelable: true }), {
      prompt, userChoice: Promise.resolve({ outcome, platform: 'web' })
    });
    browser.dispatchEvent(event);
    return prompt;
  }

  function start(): void {
    service.initialize();
    flushMicrotasks();
    offerPrompt();
    void service.requestInstallation();
    flushMicrotasks();
  }

  it('keeps loading for five seconds after the browser event without claiming Android completion', fakeAsync(() => {
    start();
    browser.dispatchEvent(new Event('appinstalled'));
    flushMicrotasks();
    expect(service.state()).not.toBe('installed');
    expect(service.busy()).toBeTrue();
    expect(notifications.show).not.toHaveBeenCalled();
    tick(4999);
    expect(service.busy()).toBeTrue();
    expect(notifications.show).not.toHaveBeenCalled();
    tick(1);
    expect(service.busy()).toBeFalse();
    expect(service.description()).toContain('Chrome ha registrado');
    expect(service.showAction()).toBeFalse();
    expect(service.highlightMessage()).toBeTrue();
    expect(service.description()).toContain('mejor experiencia');
    expect(notifications.show).toHaveBeenCalledWith('Instalación registrada', service.description(), 'info');
    tick(60000);
    expect(service.busy()).toBeFalse();
    expect(notifications.show).toHaveBeenCalledTimes(1);
    TestBed.resetTestingModule();
  }));

  it('recognizes the PWA by its id when the browser omits the manifest URL', fakeAsync(() => {
    start();
    check.and.resolveTo([{ platform: 'webapp', id: 'https://sorteos.example/' }]);
    tick(3000);
    expect(service.state()).toBe('installed');
    expect(service.busy()).toBeTrue();
    tick(5000);
    expect(service.busy()).toBeFalse();
  }));

  it('keeps browser-reported installation separate from unrelated app identities', fakeAsync(() => {
    start();
    check.and.resolveTo([{ platform: 'webapp', id: 'https://other.example/' }]);
    browser.dispatchEvent(new Event('appinstalled'));
    flushMicrotasks();
    tick(60000);
    expect(service.state()).not.toBe('installed');
    expect(service.description()).toContain('Chrome ha registrado');
    expect(service.showAction()).toBeFalse();
    expect(service.highlightMessage()).toBeTrue();
    expect(service.description()).toContain('mejor experiencia');
    expect(notifications.show).toHaveBeenCalledTimes(1);
  }));

  it('handles appinstalled before userChoice and duplicate events without restarting the five-second delay', fakeAsync(() => {
    service.initialize();
    flushMicrotasks();
    offerPrompt();
    void service.requestInstallation();
    browser.dispatchEvent(new Event('appinstalled'));
    browser.dispatchEvent(new Event('appinstalled'));
    flushMicrotasks();
    expect(service.busy()).toBeTrue();
    tick(5000);
    expect(service.busy()).toBeFalse();
    tick(60000);
    expect(service.description()).toContain('Chrome ha registrado');
    expect(service.showAction()).toBeFalse();
    expect(service.highlightMessage()).toBeTrue();
    expect(service.description()).toContain('mejor experiencia');
    expect(notifications.show).toHaveBeenCalledTimes(1);
  }));

  it('can still confirm installation after the browser event and empty query results', fakeAsync(() => {
    start();
    browser.dispatchEvent(new Event('appinstalled'));
    flushMicrotasks();
    tick(9000);
    check.and.resolveTo([{ platform: 'webapp', url: '/manifest.webmanifest' }]);
    tick(3000);
    expect(service.state()).toBe('installed');
    tick(5000);
    // The earlier browser notice is not followed by a duplicate completion dialog.
    expect(notifications.show).toHaveBeenCalledTimes(1);
    expect(notifications.show.calls.mostRecent().args[0]).toBe('Instalación registrada');
  }));
  it('confirms only the matching installed PWA and stops polling', fakeAsync(() => {
    start();
    check.and.resolveTo([{ platform: 'webapp', url: 'https://sorteos.example/manifest.webmanifest' }]);
    tick(3000);
    expect(service.state()).toBe('installed');
    expect(service.busy()).toBeTrue();
    tick(5000);
    expect(service.busy()).toBeFalse();
    expect(service.available()).toBeTrue();
    expect(notifications.show).toHaveBeenCalledWith('Aplicación instalada',
      'Sorteos GB está instalada. Ya puedes encontrarla en las aplicaciones de tu celular.', 'success');
    const calls = check.calls.count();
    tick(60000);
    expect(check.calls.count()).toBe(calls);
  }));

  it('shows the delay at 30 seconds, stops at 60 and retries verification without installing again', fakeAsync(() => {
    service.initialize();
    flushMicrotasks();
    const prompt = offerPrompt();
    void service.requestInstallation();
    flushMicrotasks();
    tick(30000);
    expect(service.description()).toContain('tardando');
    tick(30000);
    expect(service.busy()).toBeFalse();
    expect(service.actionLabel()).toBe('Comprobar nuevamente');
    expect(notifications.show).toHaveBeenCalledWith('Instalación sin confirmar', service.description(), 'warning');
    void service.requestInstallation();
    flushMicrotasks();
    expect(service.busy()).toBeTrue();
    expect(prompt).toHaveBeenCalledTimes(1);
    TestBed.resetTestingModule();
  }));

  it('does not accumulate browser requests or wait forever if the API hangs', fakeAsync(() => {
    check.and.returnValue(new Promise(() => {}));
    service.initialize();
    offerPrompt();
    void service.requestInstallation();
    flushMicrotasks();
    tick(63000);
    expect(check).toHaveBeenCalledTimes(1);
    expect(service.busy()).toBeFalse();
    expect(service.actionLabel()).toBe('Comprobar nuevamente');
    TestBed.resetTestingModule();
  }));

  it('pauses queries while hidden and checks immediately when visible again', fakeAsync(() => {
    start();
    const calls = check.calls.count();
    hidden = true;
    tick(9000);
    expect(check.calls.count()).toBe(calls);
    hidden = false;
    check.and.resolveTo([{ platform: 'webapp', url: 'https://sorteos.example/manifest.webmanifest' }]);
    page.dispatchEvent(new Event('visibilitychange'));
    flushMicrotasks();
    expect(service.state()).toBe('installed');
    tick(5000);
  }));

  it('ignores unrelated apps and handles browser query errors', fakeAsync(() => {
    check.and.resolveTo([{ platform: 'webapp', url: 'https://other.example/manifest.webmanifest' }]);
    start();
    expect(service.state()).not.toBe('installed');
    check.and.rejectWith(new Error('Unavailable'));
    tick(60000);
    expect(service.actionLabel()).toBe('Comprobar nuevamente');
  }));

  it('reports acceptance without claiming success when verification is unsupported', fakeAsync(() => {
    Object.assign(browser, { navigator: { userAgent: 'Android Chrome/140.0', platform: 'Linux', maxTouchPoints: 1 } });
    start();
    expect(service.busy()).toBeFalse();
    expect(service.state()).not.toBe('installed');
    expect(service.description()).toContain('no permite confirmarla');
    expect(notifications.show).toHaveBeenCalledWith('Solicitud de instalación aceptada', service.description(), 'info');
  }));

  it('restores the normal action after dismissal and prevents repeated prompt clicks', fakeAsync(() => {
    service.initialize();
    flushMicrotasks();
    const prompt = offerPrompt('dismissed');
    void service.requestInstallation();
    void service.requestInstallation();
    flushMicrotasks();
    expect(prompt).toHaveBeenCalledTimes(1);
    expect(service.busy()).toBeFalse();
    expect(service.state()).toBe('android-manual');
    expect(notifications.show).not.toHaveBeenCalled();
  }));

  it('hides the promotion inside the app and does not show a dialog on initial detection', fakeAsync(() => {
    standalone = true;
    service.initialize();
    flushMicrotasks();
    expect(service.state()).toBe('installed');
    expect(service.available()).toBeFalse();
    expect(check).not.toHaveBeenCalled();
    expect(notifications.show).not.toHaveBeenCalled();
  }));

  it('remembers browser registration on a later visit without asserting current installation', fakeAsync(() => {
    storage.set('sorteos-gb-installation-registered', 'registered');
    service.initialize();
    flushMicrotasks();
    expect(service.title()).toBe('Instalación registrada por Chrome');
    expect(service.showAction()).toBeFalse();
    expect(service.highlightMessage()).toBeTrue();
    expect(service.actionLabel()).toBe('Comprobar nuevamente');
    expect(service.state()).not.toBe('installed');
    expect(notifications.show).not.toHaveBeenCalled();
    offerPrompt();
    expect(storage.size).toBe(0);
    expect(service.actionLabel()).toBe('Instalar aplicación');
    expect(service.showAction()).toBeTrue();
    expect(service.highlightInstall()).toBeTrue();
    expect(service.highlightMessage()).toBeFalse();
  }));

  it('waits five seconds from actual confirmation and shows only one completion dialog', fakeAsync(() => {
    start();
    browser.dispatchEvent(new Event('appinstalled'));
    flushMicrotasks();
    tick(2000);
    check.and.resolveTo([{ platform: 'webapp', id: '/' }]);
    tick(1000);
    expect(service.state()).toBe('installed');
    tick(4999);
    expect(service.busy()).toBeTrue();
    expect(notifications.show).not.toHaveBeenCalled();
    tick(1);
    expect(service.busy()).toBeFalse();
    expect(notifications.show).toHaveBeenCalledTimes(1);
    expect(notifications.show.calls.mostRecent().args[0]).toBe('Aplicación instalada');
  }));

  it('cancels the delayed message when the service is destroyed', fakeAsync(() => {
    start();
    browser.dispatchEvent(new Event('appinstalled'));
    flushMicrotasks();
    tick(1000);
    TestBed.resetTestingModule();
    tick(60000);
    expect(notifications.show).not.toHaveBeenCalled();
  }));
  it('cleans up polling and listeners on destruction', fakeAsync(() => {
    start();
    TestBed.resetTestingModule();
    const calls = check.calls.count();
    tick(60000);
    browser.dispatchEvent(new Event('focus'));
    flushMicrotasks();
    expect(check.calls.count()).toBe(calls);
    expect(notifications.show).not.toHaveBeenCalled();
  }));
});



