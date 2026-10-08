import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { Observable } from 'rxjs';
import {
  ApiOperationResponse,
  LoginRequest,
  LoginConfirmationRequest,
  LoginResponse,
  RegisterRequest,
  ResetPasswordRequest,
} from '../core/auth/auth.models';
import { SessionService } from '../core/auth/session.service';
import { environment } from '../../environments/environment';
import { CoreService } from './core.service';

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  private readonly apiUrl = environment.apiUrl;

  constructor(
    private readonly http: HttpClient,
    private readonly session: SessionService,
    private readonly router: Router,
    private readonly settings: CoreService
  ) {}

  login(credentials: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.apiUrl}/auth/login`, credentials);
  }

  checkSession(): Observable<void> {
    return this.http.get<void>(`${this.apiUrl}/auth/session`);
  }

  getTheme(): Observable<{ isDark: boolean }> {
    return this.http.get<{ isDark: boolean }>(`${this.apiUrl}/auth/theme`);
  }

  updateTheme(isDark: boolean): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/auth/theme`, { isDark });
  }

  resendLoginConfirmation(credentials: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(
      `${this.apiUrl}/auth/login/resend-confirmation`,
      credentials
    );
  }

  confirmLogin(credentials: LoginConfirmationRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.apiUrl}/auth/login/confirm`, credentials);
  }

  register(data: RegisterRequest): Observable<ApiOperationResponse> {
    return this.http.post<ApiOperationResponse>(`${this.apiUrl}/auth/createusers`, data);
  }

  sendResetPin(whatsApp: string): Observable<ApiOperationResponse> {
    const data: ResetPasswordRequest = {
      whatsApp,
      password: '',
      confirmPassword: '',
      confirmCode: '',
    };

    return this.http.post<ApiOperationResponse>(`${this.apiUrl}/auth/SendResetPin`, data);
  }

  resetPassword(
    whatsApp: string,
    password: string,
    confirmPassword: string,
    confirmCode: string
  ): Observable<ApiOperationResponse> {
    const data: ResetPasswordRequest = {
      whatsApp,
      password,
      confirmPassword,
      confirmCode,
    };

    return this.http.post<ApiOperationResponse>(`${this.apiUrl}/auth/ResetPassword`, data);
  }

  createSession(response: LoginResponse, keepSession: boolean): void {
    this.session.saveLogin(response, keepSession);
    this.settings.setOptions({ theme: response.isDark === false ? 'light' : 'dark' });
  }

  logout(): void {
    const token = this.session.token;
    if (token) {
      this.http.post<void>(`${this.apiUrl}/auth/logout`, null, {
        headers: { Authorization: `Bearer ${token}` },
      }).subscribe({ error: () => undefined });
    }

    this.session.clear();
    this.settings.setOptions({ theme: 'dark' });
    void this.router.navigate(['/authentication/login']);
  }

  isAuthenticated(): boolean {
    return this.session.isAuthenticated();
  }

  get currentUser(): string {
    return this.session.userName;
  }

  get currentRole(): string | null {
    return this.session.roleName;
  }
}
