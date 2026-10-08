import { SessionService } from './session.service';

const keys = ['token', 'expire', 'languaje', 'rolId', 'rolName', 'user', 'userId', 'diffTime', 'keepSession', 'isDark'];

function token(sessionId: string): string {
  const payload = btoa(JSON.stringify({ jti: sessionId, exp: Math.floor(Date.now() / 1000) + 3600 }))
    .replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  return `header.${payload}.signature`;
}

describe('SessionService across tabs', () => {
  beforeEach(() => {
    for (const key of keys) {
      localStorage.removeItem(key);
      sessionStorage.removeItem(key);
    }
  });

  afterEach(() => {
    for (const key of keys) {
      localStorage.removeItem(key);
      sessionStorage.removeItem(key);
    }
  });

  it('keeps the old tab token pinned and does not erase a newer token', () => {
    const first = new SessionService();
    const firstToken = token(crypto.randomUUID());
    const secondToken = token(crypto.randomUUID());
    first.saveLogin({ token: firstToken, userId: 5 }, true);

    localStorage.setItem('token', secondToken);
    expect(first.token).toBe(firstToken);

    first.invalidateIfCurrent(firstToken, 'replaced');
    expect(first.token).toBeNull();
    expect(localStorage.getItem('token')).toBe(secondToken);
  });

  it('closes the previous tab after a new login and keeps the new login', async () => {
    const first = new SessionService();
    first.saveLogin({ token: token(crypto.randomUUID()), userId: 5 }, true);
    const second = new SessionService();
    const secondToken = token(crypto.randomUUID());
    second.saveLogin({ token: secondToken, userId: 5 }, true);

    await new Promise(resolve => setTimeout(resolve, 50));

    expect(first.token).toBeNull();
    expect(second.token).toBe(secondToken);
    expect(localStorage.getItem('token')).toBe(secondToken);
  });

  it('does not allow a newly opened tab to share an active session', async () => {
    const first = new SessionService();
    const firstToken = token(crypto.randomUUID());
    first.saveLogin({ token: firstToken, userId: 5123456 }, true);
    await new Promise(resolve => setTimeout(resolve, 10));
    const duplicate = new SessionService();

    await duplicate.whenReady();

    expect(first.token).toBe(firstToken);
    expect(duplicate.token).toBeNull();
    expect(localStorage.getItem('token')).toBe(firstToken);
  });

  it('restores a persistent session in a new tab with no previous tab active', async () => {
    const savedToken = token(crypto.randomUUID());
    localStorage.setItem('token', savedToken);
    localStorage.setItem('userId', '9911');
    localStorage.setItem('keepSession', 'true');

    const reopened = new SessionService();
    await reopened.whenReady();

    expect(reopened.isAuthenticated()).toBeTrue();
    expect(reopened.snapshot?.keepSession).toBeTrue();
    expect(reopened.token).toBe(savedToken);
  });

  it('keeps the selected theme with the active session and clears it on logout', () => {
    const session = new SessionService();
    session.saveLogin({ token: token(crypto.randomUUID()), userId: 5, isDark: false }, true);

    expect(session.snapshot?.isDark).toBeFalse();
    expect(localStorage.getItem('isDark')).toBe('false');

    session.setTheme(true);
    expect(session.snapshot?.isDark).toBeTrue();
    expect(localStorage.getItem('isDark')).toBe('true');

    session.clear();
    expect(localStorage.getItem('isDark')).toBeNull();
  });
});
