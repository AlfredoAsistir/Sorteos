import { environment } from '../../../environments/environment';

export function paymentInstructionApiUrl(code: string | null): string | null {
  const normalizedCode = code?.trim() ?? '';
  if (!/^[0-9A-Za-z_-]+$/.test(normalizedCode)) return null;

  return `${environment.apiUrl}/p/${encodeURIComponent(normalizedCode)}`;
}

export function paymentInstructionApiUrlFromPublicUrl(publicUrl: string): string | null {
  try {
    const url = new URL(publicUrl);
    const match = /^\/p\/([0-9A-Za-z_-]+)$/.exec(url.pathname);
    if (!/^https?:$/.test(url.protocol) || !match || url.search || url.hash) return null;

    return paymentInstructionApiUrl(match[1]);
  } catch {
    return null;
  }
}
