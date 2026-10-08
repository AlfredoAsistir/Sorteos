import {
  APP_VERSION_PLACEHOLDER,
  getEmbeddedAppVersion,
  isAppUpdateAvailable,
  normalizeAppVersion
} from './app-update.utils';

describe('app update utilities', () => {
  it('normalizes a valid build version', () => {
    expect(normalizeAppVersion(' 5.0.0+20260925 ')).toBe('5.0.0+20260925');
  });

  it('ignores the development placeholder', () => {
    expect(normalizeAppVersion(APP_VERSION_PLACEHOLDER)).toBeNull();
  });

  it('reads the embedded version from the document metadata', () => {
    const document = new DOMParser().parseFromString(
      '<meta name="app-version" content="5.0.0+build-1">',
      'text/html'
    );
    expect(getEmbeddedAppVersion(document)).toBe('5.0.0+build-1');
  });

  it('detects a different deployed version', () => {
    expect(isAppUpdateAvailable('5.0.0+build-1', '5.0.0+build-2')).toBeTrue();
  });

  it('does not report an update without a stamped current version', () => {
    expect(isAppUpdateAvailable(null, '5.0.0+build-2')).toBeFalse();
  });
});
