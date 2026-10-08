export type AppInstallationState =
  | 'installed'
  | 'android-ready'
  | 'android-manual'
  | 'ios-safari'
  | 'ios-other-browser'
  | 'unsupported';

export type AppNavigator = Pick<Navigator, 'userAgent' | 'platform' | 'maxTouchPoints'> & {
  standalone?: boolean;
};

export function isIosDevice(appNavigator: AppNavigator): boolean {
  return /iPhone|iPad|iPod/i.test(appNavigator.userAgent) ||
    (appNavigator.platform === 'MacIntel' && appNavigator.maxTouchPoints > 1);
}

export function isAndroidDevice(appNavigator: AppNavigator): boolean {
  return /Android/i.test(appNavigator.userAgent);
}

export function isChromeOnAndroid(appNavigator: AppNavigator): boolean {
  return isAndroidDevice(appNavigator) &&
    /Chrome\//i.test(appNavigator.userAgent) &&
    !/EdgA|OPR\//i.test(appNavigator.userAgent);
}

export function isSafariOnIos(appNavigator: AppNavigator): boolean {
  return isIosDevice(appNavigator) &&
    /Safari/i.test(appNavigator.userAgent) &&
    !/CriOS|FxiOS|EdgiOS|OPiOS/i.test(appNavigator.userAgent);
}

export function isInstalledMobileApp(
  appWindow: Pick<Window, 'matchMedia'>,
  appNavigator: AppNavigator
): boolean {
  const isStandalone = appWindow.matchMedia('(display-mode: standalone)').matches ||
    appNavigator.standalone === true;
  return isStandalone && (isAndroidDevice(appNavigator) || isIosDevice(appNavigator));
}

export function getInstallationState(
  appWindow: Pick<Window, 'matchMedia'>,
  appNavigator: AppNavigator,
  androidPromptReady: boolean
): AppInstallationState {
  if (isInstalledMobileApp(appWindow, appNavigator)) return 'installed';
  if (isIosDevice(appNavigator)) {
    return isSafariOnIos(appNavigator) ? 'ios-safari' : 'ios-other-browser';
  }
  if (isChromeOnAndroid(appNavigator)) {
    return androidPromptReady ? 'android-ready' : 'android-manual';
  }
  return 'unsupported';
}
