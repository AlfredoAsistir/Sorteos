import { isInstalledMobileApp } from './installed-mobile-app';

function appWindow(standalone: boolean): Pick<Window, 'matchMedia'> {
  return { matchMedia: () => ({ matches: standalone }) as MediaQueryList };
}

function appNavigator(userAgent: string, standalone = false, platform = '', maxTouchPoints = 0):
  Pick<Navigator, 'userAgent' | 'platform' | 'maxTouchPoints'> & { standalone?: boolean } {
  return { userAgent, standalone, platform, maxTouchPoints };
}

describe('isInstalledMobileApp', () => {
  it('detects an installed Android app', () => {
    expect(isInstalledMobileApp(appWindow(true), appNavigator('Mozilla/5.0 (Linux; Android 14)'))).toBeTrue();
  });

  it('detects an installed iPhone app through the Safari fallback', () => {
    expect(isInstalledMobileApp(appWindow(false), appNavigator('Mozilla/5.0 (iPhone; CPU iPhone OS 17_0)', true))).toBeTrue();
  });

  it('detects an installed iPad app with a desktop-style user agent', () => {
    expect(isInstalledMobileApp(appWindow(true), appNavigator('Mozilla/5.0 (Macintosh)', false, 'MacIntel', 5))).toBeTrue();
  });

  it('does not select the default in a mobile browser tab', () => {
    expect(isInstalledMobileApp(appWindow(false), appNavigator('Mozilla/5.0 (Linux; Android 14)'))).toBeFalse();
  });

  it('does not select the default in a desktop installed app', () => {
    expect(isInstalledMobileApp(appWindow(true), appNavigator('Mozilla/5.0 (Windows NT 10.0)'))).toBeFalse();
  });
});
