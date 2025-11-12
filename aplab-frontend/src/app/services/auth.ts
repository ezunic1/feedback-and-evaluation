import { Injectable, signal, inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, Subject, throwError, of } from 'rxjs';
import { catchError, tap, map, shareReplay, switchMap } from 'rxjs/operators';

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

  private refreshInFlight$?: Observable<string | null>;
  private wakeUp$ = new Subject<void>();

  constructor(private http: HttpClient) {
    if (this.isBrowser) {
      const at = this.getItem('access_token');
      this._isLoggedIn.set(!!at);
      this._user.set(at ? this.decodeUser(at) : null);
      this.scheduleProactiveRefresh();
    }
  }

  isLoggedIn() { return this._isLoggedIn(); }
  user() { return this._user(); }

  register(data: RegisterRequest): Observable<any> {
    const payload: RegisterRequest = {
      fullName: (data.fullName || '').trim(),
      email: (data.email || '').trim().toLowerCase(),
      password: data.password || ''
    };
    return this.http.post(`${this.api}/register`, payload).pipe(
      catchError((err: HttpErrorResponse) => {
        const detail = (err?.error && typeof err.error === 'object') ? (err.error.detail || err.error.message) : null;
        const str = typeof err?.error === 'string' ? err.error : null;
        const msg = detail || str || (err.status === 409 ? 'A user with this email already exists.' : err.status === 400 ? 'Invalid input.' : `Request failed (${err.status})`);
        return throwError(() => new Error(msg));
      })
    );
  }

  login(data: LoginRequest): Observable<TokenResponse> {
    const raw = (data.usernameOrEmail || '').trim();
    const payload: LoginRequest = {
      usernameOrEmail: raw.includes('@') ? raw.toLowerCase() : raw,
      password: (data.password || '').trim()
    };
    return this.http.post<any>(`${this.api}/login`, payload).pipe(
      map(res => {
        if (res && typeof res === 'object' && 'access_token' in res) return res as TokenResponse;
        if (res?.token && typeof res.token === 'object') return res.token as TokenResponse;
        throw new Error('Unexpected login response.');
      }),
      tap(t => {
        if (this.isBrowser) {
          this.setItem('access_token', t.access_token ?? '');
          this.setItem('refresh_token', t.refresh_token ?? '');
        }
        this._isLoggedIn.set(true);
        this._user.set(this.decodeUser(t.access_token));
        this.scheduleProactiveRefresh(true);
      }),
      tap(() => this.sync$().subscribe()),
      catchError((err: HttpErrorResponse) => {
  if (err.status === 428) {
    const raw = err.error || {};
    const url = raw.changePasswordUrl || raw.authUrl || raw.AuthUrl || '';
    const message = raw.message || 'You must change your password before the first login.';
    return throwError(() => ({ changeRequired: true, url, message }));
  }

  const detail = (err?.error && typeof err.error === 'object') ? (err.error.detail || err.error.message) : null;
  const str = typeof err?.error === 'string' ? err.error : null;
  const msg = detail || str || `Request failed (${err.status})`;
  return throwError(() => new Error(msg));
})

    );
  }

  logout() {
    if (this.isBrowser) {
      this.removeItem('access_token');
      this.removeItem('refresh_token');
    }
    this._isLoggedIn.set(false);
    this._user.set(null);
    this.refreshInFlight$ = undefined;
    this.wakeUp$.next();
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

  getValidAccessToken$(): Observable<string | null> {
    if (!this.isBrowser) return of(null);
    const token = this.accessToken;
    if (token && !this.isExpiringSoon(token)) return of(token);
    if (this.refreshInFlight$) return this.refreshInFlight$;
    this.refreshInFlight$ = this.refreshToken$().pipe(
      tap(() => (this.refreshInFlight$ = undefined)),
      shareReplay(1)
    );
    return this.refreshInFlight$;
  }

  private refreshToken$(): Observable<string | null> {
    if (!this.isBrowser) return of(null);
    const rt = this.getItem('refresh_token');
    if (!rt) return of(null);
    return of(null);
  }

  private sync$(): Observable<any> {
    return this.http.post(`${this.api}/sync`, {});
  }

  private scheduleProactiveRefresh(restart = false) {
    if (!this.isBrowser) return;
    if (restart) this.wakeUp$.next();
    const token = this.accessToken;
    if (!token) return;
    const nowSec = Math.floor(Date.now() / 1000);
    const exp = this.readExp(token) ?? nowSec + 60;
    const msUntil = Math.max((exp - nowSec - 30) * 1000, 0);
    const delay = Math.min(Math.max(msUntil, 5000), 86400000);
    const cancel = { cancelled: false };
    const sub = this.wakeUp$.subscribe(() => (cancel.cancelled = true));
    setTimeout(() => {
      sub.unsubscribe();
      if (!cancel.cancelled) {
        this.getValidAccessToken$().subscribe({
          next: () => this.scheduleProactiveRefresh(),
          error: () => {}
        });
      }
    }, delay);
  }

  private isExpiringSoon(token: string, withinSec = 30): boolean {
    const exp = this.readExp(token);
    if (!exp) return true;
    const now = Math.floor(Date.now() / 1000);
    return exp - now <= withinSec;
  }

  private readExp(token: string): number | null {
    try {
      const payload = JSON.parse(atob(token.split('.')[1])) as JwtPayload;
      return payload?.exp ?? null;
    } catch { return null; }
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
}
