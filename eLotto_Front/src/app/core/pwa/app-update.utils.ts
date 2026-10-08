export const APP_VERSION_META_NAME = 'app-version';
export const APP_VERSION_PLACEHOLDER = '__APP_VERSION__';

export interface AppVersionInfo {
  version: string;
  generatedAt?: string;
}

export function normalizeAppVersion(value: unknown): string | null {
  if (typeof value !== 'string') return null;
  const version = value.trim();
  if (!version || version === APP_VERSION_PLACEHOLDER) return null;
  return version;
}

export function getEmbeddedAppVersion(document: Document): string | null {
  const content = document
    .querySelector<HTMLMetaElement>(`meta[name="${APP_VERSION_META_NAME}"]`)
    ?.content;
  return normalizeAppVersion(content);
}

export function isAppUpdateAvailable(currentVersion: string | null, latestVersion: unknown): boolean {
  const latest = normalizeAppVersion(latestVersion);
  return currentVersion !== null && latest !== null && currentVersion !== latest;
}
