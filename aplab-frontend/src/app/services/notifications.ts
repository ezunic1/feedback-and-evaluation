import { Injectable, inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel, HttpTransportType } from '@microsoft/signalr';
import { Subject, Subscription } from 'rxjs';
import { distinctUntilChanged } from 'rxjs/operators';
import { Auth } from './auth';

@Injectable({ providedIn: 'root' })
export class Notifications {
  private hub?: HubConnection;
  private base = '';
  private auth = inject(Auth);
  private platformId = inject(PLATFORM_ID);
  private starting = false;
  private tokenSub?: Subscription;
  private retryHandle?: any;
  private backoffMs = 500;
  private readonly maxBackoffMs = 15000;

  private newFeedbackSubject = new Subject<any>();
  private deleteRequestCreatedSubject = new Subject<any>();

  newFeedback$ = this.newFeedbackSubject.asObservable();
  deleteRequestCreated$ = this.deleteRequestCreatedSubject.asObservable();

  connectWhenAuthenticated(apiBase: string): void {
    if (!isPlatformBrowser(this.platformId)) return;
    this.base = (apiBase?.trim() || '').replace(/\/+$/, '');
    this.tokenSub?.unsubscribe();
    this.tokenSub = this.auth.tokenChanges$.pipe(
      distinctUntilChanged()
    ).subscribe(token => {
      if (token) this.start();
      else this.disconnect();
    });
    const tok = this.auth.accessToken;
    if (tok) this.start();
  }

  private buildHub(): void {
    const url = `${this.base}/hubs/notifications`;
    this.hub = new HubConnectionBuilder()
      .withUrl(url, {
        accessTokenFactory: () => this.auth.accessToken ?? '',
        transport: HttpTransportType.WebSockets,
        skipNegotiation: true
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Information)
      .build();

    this.hub.on('newFeedback', p => this.newFeedbackSubject.next(p));
    this.hub.on('deleteRequestCreated', p => this.deleteRequestCreatedSubject.next(p));

    this.hub.onreconnecting(() => {});
    this.hub.onreconnected(() => { this.resetBackoff(); });
    this.hub.onclose(() => {
      if (this.auth.accessToken) this.scheduleRetry();
    });
  }

  private async start(): Promise<void> {
    if (!this.hub) this.buildHub();
    if (!this.hub) return;
    if (this.hub.state === HubConnectionState.Connected) return;
    if (!this.auth.accessToken) return;
    if (this.starting) return;

    this.starting = true;
    try {
      await this.hub.start();
      this.resetBackoff();
    } catch {
      this.scheduleRetry();
    } finally {
      this.starting = false;
    }
  }

  private scheduleRetry(): void {
    if (this.retryHandle) return;
    this.retryHandle = setTimeout(() => {
      this.retryHandle = undefined;
      this.start();
    }, this.backoffMs);
    this.backoffMs = Math.min(this.backoffMs * 2, this.maxBackoffMs);
  }

  private resetBackoff(): void {
    if (this.retryHandle) {
      clearTimeout(this.retryHandle);
      this.retryHandle = undefined;
    }
    this.backoffMs = 500;
  }

  disconnect(): void {
    this.resetBackoff();
    this.hub?.stop().catch(() => {});
    this.hub = undefined;
  }

  isConnected(): boolean {
    return !!this.hub && this.hub.state === HubConnectionState.Connected;
  }
}
