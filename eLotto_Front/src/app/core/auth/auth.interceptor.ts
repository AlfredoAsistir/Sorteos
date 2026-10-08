import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SessionService } from './session.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const session = inject(SessionService);
  const isApiRequest = request.url.startsWith(environment.apiUrl);
  const isAuthenticationEndpoint = request.url.startsWith(`${environment.apiUrl}/auth/`) &&
    request.url !== `${environment.apiUrl}/auth/session` &&
    request.url !== `${environment.apiUrl}/auth/theme`;

  if (!isApiRequest || isAuthenticationEndpoint) return next(request);

  return from(session.whenReady()).pipe(
    switchMap(() => {
      if (!session.isAuthenticated()) return next(request);
      const token = session.token;
      if (!token) return next(request);

      return next(request.clone({
        setHeaders: { Authorization: `Bearer ${token}` },
      })).pipe(
        catchError((error: unknown) => {
          if (error instanceof HttpErrorResponse && error.status === 401 &&
              (error.error?.code ?? error.error?.Code) === 'session_replaced') {
            session.invalidateIfCurrent(token, 'replaced');
          }
          return throwError(() => error);
        })
      );
    })
  );
};