import { Injectable, signal, inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError, tap } from 'rxjs/operators';

export type RegisterRequest = { fullName: string; email: string; password: string; };
export type LoginRequest = { usernameOrEmail: string; password: string; };

type TokenResponse = {
  access_token: string;
  refresh_token: string;
  token_type: string;
  expires_in: number;
  refresh_expires_in: number;
  scope?: string;
};

type JwtPayload = {
  sub?: string;
  email?: string;
  preferred_username?: string;
  name?: string;
  roles?: string[];
  realm_access?: { roles?: string[] };
  resource_access?: Record<string, { roles?: string[] }>;
  groups?: string[];
  exp?: number;
  [k: string]: any;
};

@Injectable({ providedIn: 'root' })
export class Auth {
  private readonly api = '/api/auth';

  private platformId = inject(PLATFORM_ID);
  private readonly isBrowser = isPlatformBrowser(this.platformId);

  private _isLoggedIn = signal<boolean>(false);
  private _user = signal<{ name?: string; email?: string } | null>(null);

  constructor(private http: HttpClient) {
    if (this.isBrowser) {
      const at = this.getItem('access_token');
      this._isLoggedIn.set(!!at);
      this._user.set(at ? this.decodeUser(at) : null);
    }
  }

  isLoggedIn() { return this._isLoggedIn(); }
  user() { return this._user(); }

  register(data: RegisterRequest): Observable<any> {
    return this.http.post(`${this.api}/register`, data).pipe(
      catchError(this.handleError)
    );
  }

  login(data: LoginRequest): Observable<TokenResponse> {
    return this.http.post<TokenResponse>(`${this.api}/login`, data).pipe(
      tap(t => {
        if (this.isBrowser) {
          this.setItem('access_token', t.access_token ?? '');
          this.setItem('refresh_token', t.refresh_token ?? '');
        }
        this._isLoggedIn.set(true);
        this._user.set(this.decodeUser(t.access_token));
      }),
      catchError(this.handleError)
    );
  }

  logout() {
    if (this.isBrowser) {
      this.removeItem('access_token');
      this.removeItem('refresh_token');
    }
    this._isLoggedIn.set(false);
    this._user.set(null);
  }

  get accessToken(): string | null {
    return this.isBrowser ? this.getItem('access_token') : null;
  }

  roles(): string[] {
    const token = this.accessToken;
    if (!token) return [];
    try {
      const p = JSON.parse(atob(token.split('.')[1])) as JwtPayload;
      const out: string[] = [];

      if (Array.isArray(p.roles)) out.push(...p.roles);
      if (p.realm_access?.roles?.length) out.push(...p.realm_access.roles);

      if (p.resource_access) {
        for (const k of Object.keys(p.resource_access)) {
          const rr = p.resource_access[k]?.roles ?? [];
          if (rr.length) out.push(...rr);
        }
      }

      if (Array.isArray(p.groups)) {
        for (const g of p.groups) {
          const last = g?.split('/').filter(Boolean).pop();
          if (last) out.push(last);
        }
      }

      // normalizacija i uniq
      const norm = out.map(r => r.toLowerCase());
      return Array.from(new Set(norm));
    } catch {
      return [];
    }
  }

  role(): string | null {
    const rs = this.roles();
    const order = ['admin', 'mentor', 'intern', 'guest'];
    for (const r of order) if (rs.includes(r)) return r;
    return rs[0] ?? null;
  }

  hasRole(r: string): boolean {
    return this.roles().includes(r.toLowerCase());
  }

  userId(): string | null {
    const token = this.accessToken;
    if (!token) return null;
    try {
      const p = JSON.parse(atob(token.split('.')[1])) as JwtPayload;
      return p.sub ?? null;
    } catch {
      return null;
    }
  }

  private handleError(err: HttpErrorResponse) {
    const msg = typeof err.error === 'string' && err.error ? err.error
      : err.error?.message ?? `Request failed (${err.status})`;
    return throwError(() => new Error(msg));
  }

  private decodeUser(token: string | null) {
    if (!token) return null;
    try {
      const payload = JSON.parse(atob(token.split('.')[1])) as JwtPayload;
      return {
        name: payload.name ?? payload.preferred_username ?? undefined,
        email: payload.email ?? undefined
      };
    } catch {
      return null;
    }
  }

  private getItem(key: string): string | null {
    try { return localStorage.getItem(key); } catch { return null; }
  }
  private setItem(key: string, value: string) {
    try { localStorage.setItem(key, value); } catch {}
  }
  private removeItem(key: string) {
    try { localStorage.removeItem(key); } catch {}
  }
}
