import { AppNavigator, getInstallationState } from './app-installation.utils';

function appWindow(standalone: boolean): Pick<Window, 'matchMedia'> {
  return { matchMedia: () => ({ matches: standalone }) as MediaQueryList };
}

function appNavigator(
  userAgent: string,
  standalone = false,
  platform = '',
  maxTouchPoints = 0
): AppNavigator {
  return { userAgent, standalone, platform, maxTouchPoints };
}

describe('app installation state', () => {
  it('offers the native prompt when Chrome Android reports it ready', () => {
    const navigator = appNavigator('Mozilla/5.0 (Linux; Android 14) Chrome/140.0 Mobile Safari/537.36');
    expect(getInstallationState(appWindow(false), navigator, true)).toBe('android-ready');
  });

  it('offers manual Chrome instructions while the native prompt is unavailable', () => {
    const navigator = appNavigator('Mozilla/5.0 (Linux; Android 14) Chrome/140.0 Mobile Safari/537.36');
    expect(getInstallationState(appWindow(false), navigator, false)).toBe('android-manual');
  });

  it('offers Safari instructions on iPhone', () => {
    const navigator = appNavigator('Mozilla/5.0 (iPhone; CPU iPhone OS 18_0) Version/18.0 Mobile Safari/604.1');
    expect(getInstallationState(appWindow(false), navigator, false)).toBe('ios-safari');
  });

  it('asks an iPhone Chrome user to continue in Safari', () => {
    const navigator = appNavigator('Mozilla/5.0 (iPhone; CPU iPhone OS 18_0) CriOS/140.0 Mobile/15E148 Safari/604.1');
    expect(getInstallationState(appWindow(false), navigator, false)).toBe('ios-other-browser');
  });

  it('recognizes an installed iPad using a desktop-style user agent', () => {
    const navigator = appNavigator('Mozilla/5.0 (Macintosh)', false, 'MacIntel', 5);
    expect(getInstallationState(appWindow(true), navigator, false)).toBe('installed');
  });

  it('does not promote installation on unsupported desktop browsers', () => {
    const navigator = appNavigator('Mozilla/5.0 (Windows NT 10.0) Chrome/140.0');
    expect(getInstallationState(appWindow(false), navigator, false)).toBe('unsupported');
  });
});
