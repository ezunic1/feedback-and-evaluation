import { Injectable, inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel, HttpTransportType } from '@microsoft/signalr';
import { Subject, Observable, of, take, timeout, catchError } from 'rxjs';
import { Auth } from './auth';

@Injectable({ providedIn: 'root' })
export class Notifications {
  private hub?: HubConnection;
  private auth = inject(Auth);
  private platformId = inject(PLATFORM_ID);

  private newFeedbackSubject = new Subject<any>();
  private deleteRequestCreatedSubject = new Subject<any>();

  newFeedback$ = this.newFeedbackSubject.asObservable();
  deleteRequestCreated$ = this.deleteRequestCreatedSubject.asObservable();

  connect(apiBase: string): void {
    if (!isPlatformBrowser(this.platformId)) return;
    if (this.hub) return;

    const base = (apiBase?.trim() || 'https://localhost:7062').replace(/\/+$/, '');
    const url = `${base}/hubs/notifications`;

    this.hub = new HubConnectionBuilder()
      .withUrl(url, {
        accessTokenFactory: () => this.auth.accessToken ?? '',
        transport: HttpTransportType.WebSockets,
        skipNegotiation: true,
        withCredentials: true
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Information)
      .build();

    this.hub.on('newFeedback', p => {
      console.info('[SR] newFeedback', p);
      this.newFeedbackSubject.next(p);
    });

    this.hub.on('deleteRequestCreated', p => {
      console.info('[SR] deleteRequestCreated', p);
      this.deleteRequestCreatedSubject.next(p);
    });

    this.hub.onreconnecting(err => console.warn('[SR] reconnecting', err));
    this.hub.onreconnected(id => console.info('[SR] reconnected', id));
    this.hub.onclose(err => console.error('[SR] closed', err));

    const start = () => {
      if (!this.hub) return;
      console.info('[SR] starting hub to', url);
      this.hub.start()
        .then(() => {
          console.info('[SR] connected:', this.hub!.state);
          // ako si dodao Ping() na backend hub:
          // this.hub!.invoke('Ping').then(r => console.info('[SR] ping:', r)).catch(e => console.error('[SR] ping error', e));
        })
        .catch(err => console.error('[SR] start error', err));
    };

    if (this.auth.accessToken) {
      start();
    } else {
      let once$: Observable<any>;
      try {
        once$ = this.auth.getValidAccessToken$().pipe(
          take(1),
          timeout(4000),
          catchError(() => of(null))
        );
      } catch {
        once$ = of(null);
      }
      once$.subscribe(() => start());
    }
  }

  disconnect(): void {
    this.hub?.stop().catch(() => {});
    this.hub = undefined;
  }

  isConnected(): boolean {
    return !!this.hub && this.hub.state === HubConnectionState.Connected;
  }
}
