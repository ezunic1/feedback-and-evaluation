import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Auth } from '../services/auth';
import { catchError, switchMap, throwError } from 'rxjs';

const RETRIED = Symbol('retried');

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(Auth);
  const token = auth.accessToken;
  let authReq = req;
  if (token) authReq = req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });

  return next(authReq).pipe(
    catchError((err: HttpErrorResponse) => {
      const already = (req as any)[RETRIED] === true;
      if (!(err.status === 401 || err.status === 403) || already) return throwError(() => err);
      return auth.getValidAccessToken$().pipe(
        switchMap(t => {
          const retried = req.clone({ setHeaders: t ? { Authorization: `Bearer ${t}` } : {} });
          (retried as any)[RETRIED] = true;
          return next(retried);
        }),
        catchError(e => throwError(() => (e instanceof HttpErrorResponse ? e : err)))
      );
    })
  );
};
