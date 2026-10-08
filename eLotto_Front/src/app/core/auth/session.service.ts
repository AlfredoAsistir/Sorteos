import { Injectable } from '@angular/core';
import { Subject } from 'rxjs';
import { AuthSession, LoginResponse } from './auth.models';

const SESSION_KEYS = [
  'token',
  'expire',
  'languaje',
  'rolId',
  'rolName',
  'user',
  'userId',
  'keepSession',
  'isDark',
] as const;

export type SessionEndReason = 'replaced' | 'duplicate_tab';

interface SessionMessage {
  type: 'login' | 'probe' | 'occupied';
  userId: string;
  sessionId: string;
  tabId: string;
  openedAt: number;
  targetId?: string;
}

@Injectable({ providedIn: 'root' })
export class SessionService {
  private readonly tabId = globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random()}`;
  private readonly openedAt = Date.now();
  private readonly channel = typeof BroadcastChannel === 'undefined'
    ? null
    : new BroadcastChannel('elotto-auth-session');
  private currentSession = this.readStoredSession();
  private readonly sessionEnded = new Subject<SessionEndReason>();
  readonly sessionEnded$ = this.sessionEnded.asObservable();
  private readonly ready: Promise<void>;
  private resolveReady: (() => void) | null = null;

  constructor() {
    this.channel?.addEventListener('message', event => this.handleMessage(event.data as SessionMessage));
    const sessionId = this.currentSession && this.sessionId(this.currentSession.token);
    if (this.channel && sessionId && this.currentSession?.userId) {
      this.ready = new Promise(resolve => {
        this.resolveReady = resolve;
        this.channel?.postMessage(this.message('probe', this.currentSession!.userId!, sessionId));
        window.setTimeout(() => this.finishReady(), 200);
      });
    } else {
      this.ready = Promise.resolve();
    }
  }

  whenReady(): Promise<void> { return this.ready; }

  saveLogin(response: LoginResponse, keepSession: boolean): void {
    if (!response.token) return;

    this.clear();
    for (const key of SESSION_KEYS) localStorage.removeItem(key);

    const storage = keepSession ? localStorage : sessionStorage;
    this.set(storage, 'token', response.token);
    this.set(storage, 'expire', response.expire);
    this.set(storage, 'languaje', response.languaje);
    this.set(storage, 'rolId', response.rolId);
    this.set(storage, 'rolName', response.rolName);
    this.set(storage, 'user', response.user);
    this.set(storage, 'userId', response.userId);
    this.set(storage, 'isDark', response.isDark);
    storage.setItem('keepSession', keepSession ? 'true' : 'false');
    this.currentSession = this.readStoredSession();
    this.finishReady();

    const sessionId = this.sessionId(response.token);
    if (response.userId != null && sessionId) {
      this.channel?.postMessage(this.message('login', String(response.userId), sessionId));
    }
  }

  setTheme(isDark: boolean): void {
    const current = this.currentSession;
    if (!current) return;
    const storage = sessionStorage.getItem('token') === current.token ? sessionStorage :
      localStorage.getItem('token') === current.token ? localStorage : null;
    if (!storage) return;
    storage.setItem('isDark', String(isDark));
    this.currentSession = { ...current, isDark };
  }

  clear(): void {
    const token = this.currentSession?.token;
    const clearPersistent = !!token && localStorage.getItem('token') === token;
    for (const key of SESSION_KEYS) {
      sessionStorage.removeItem(key);
      if (clearPersistent) localStorage.removeItem(key);
    }
    this.currentSession = null;
    this.finishReady();
  }

  invalidateIfCurrent(token: string, reason: SessionEndReason, preservePersistent = false): void {
    if (this.currentSession?.token !== token) return;
    if (preservePersistent) {
      for (const key of SESSION_KEYS) sessionStorage.removeItem(key);
      this.currentSession = null;
      this.finishReady();
    } else {
      this.clear();
    }
    this.sessionEnded.next(reason);
  }

  get token(): string | null { return this.currentSession?.token ?? null; }
  get userName(): string { return this.currentSession?.user || 'Usuario'; }
  get roleName(): string | null { return this.currentSession?.rolName ?? null; }
  get userId(): string | null { return this.currentSession?.userId ?? null; }
  get snapshot(): AuthSession | null { return this.currentSession; }

  isAuthenticated(): boolean {
    const token = this.token;
    if (!token) return false;

    if (this.isJwtExpired(token)) {
      this.clear();
      return false;
    }

    const expire = this.currentSession?.expire;
    if (expire && this.isExplicitExpirationExpired(expire)) {
      this.clear();
      return false;
    }

    return true;
  }

  private readStoredSession(): AuthSession | null {
    const storage = sessionStorage.getItem('token') ? sessionStorage : localStorage;
    const token = storage.getItem('token');
    if (!token) return null;
    const isDark = storage.getItem('isDark');
    return {
      token,
      expire: storage.getItem('expire') ?? undefined,
      languaje: storage.getItem('languaje') ?? undefined,
      rolId: storage.getItem('rolId') ?? undefined,
      rolName: storage.getItem('rolName') ?? undefined,
      user: storage.getItem('user') ?? undefined,
      userId: storage.getItem('userId') ?? undefined,
      isDark: isDark === null ? undefined : isDark === 'true',
      keepSession: storage.getItem('keepSession') === 'true',
    };
  }

  private set(storage: Storage, key: string, value: unknown): void {
    if (value !== undefined && value !== null) storage.setItem(key, String(value));
  }

  private sessionId(token: string): string | null {
    try {
      const payload = JSON.parse(this.decodeBase64Url(token.split('.')[1])) as { jti?: string };
      return payload.jti ?? null;
    } catch {
      return null;
    }
  }

  private message(type: SessionMessage['type'], userId: string, sessionId: string, targetId?: string): SessionMessage {
    return { type, userId, sessionId, tabId: this.tabId, openedAt: this.openedAt, targetId };
  }

  private handleMessage(message: SessionMessage): void {
    const current = this.currentSession;
    if (!current || !message || message.tabId === this.tabId ||
        message.userId !== current.userId) return;
    const sessionId = this.sessionId(current.token);
    if (!sessionId) return;

    if (message.type === 'login' && message.sessionId !== sessionId) {
      this.invalidateIfCurrent(current.token, 'replaced');
    } else if (message.type === 'probe' && message.sessionId === sessionId) {
      const ownRank = `${this.openedAt}-${this.tabId}`;
      const otherRank = `${message.openedAt}-${message.tabId}`;
      if (ownRank < otherRank) {
        this.channel?.postMessage(this.message('occupied', current.userId!, sessionId, message.tabId));
      }
    } else if (message.type === 'occupied' && message.targetId === this.tabId &&
               message.sessionId === sessionId) {
      this.invalidateIfCurrent(current.token, 'duplicate_tab', true);
    }
  }

  private finishReady(): void {
    this.resolveReady?.();
    this.resolveReady = null;
  }

  private isJwtExpired(token: string): boolean {
    const parts = token.split('.');
    if (parts.length !== 3) return false;

    try {
      const payload = JSON.parse(this.decodeBase64Url(parts[1])) as { exp?: number };
      return typeof payload.exp === 'number' && Date.now() >= payload.exp * 1000;
    } catch {
      return false;
    }
  }

  private isExplicitExpirationExpired(expire: string): boolean {
    const numeric = Number(expire);
    if (Number.isFinite(numeric) && numeric > 0) {
      const milliseconds = numeric > 10_000_000_000 ? numeric : numeric * 1000;
      return Date.now() >= milliseconds;
    }

    const parsed = Date.parse(expire);
    return Number.isFinite(parsed) ? Date.now() >= parsed : false;
  }

  private decodeBase64Url(value: string): string {
    const normalized = value.replace(/-/g, '+').replace(/_/g, '/');
    const padded = normalized.padEnd(Math.ceil(normalized.length / 4) * 4, '=');
    return decodeURIComponent(
      Array.from(atob(padded))
        .map((char) => `%${char.charCodeAt(0).toString(16).padStart(2, '0')}`)
        .join('')
    );
  }
}
