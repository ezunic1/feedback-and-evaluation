import { Component, OnDestroy, OnInit, inject, signal, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { Router, RouterOutlet } from '@angular/router';
import { ToastContainer } from './shared/toast-container/toast-container';
import { Notifications } from './services/notifications';
import { Toasts } from './services/toasts';
import { NewFeedbackEvent, DeleteRequestCreatedEvent } from './models/notifications';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, ToastContainer],
  templateUrl: './app.html',
  styleUrls: ['./app.css']
})
export class App implements OnInit, OnDestroy {
  title = signal('aplab-frontend');

  private notifications = inject(Notifications);
  private toasts = inject(Toasts);
  private router = inject(Router);
  private platformId = inject(PLATFORM_ID);
  private subs: any[] = [];

  ngOnInit(): void {
    if (isPlatformBrowser(this.platformId)) {
      queueMicrotask(() => this.notifications.connect('')); 

      this.subs.push(
        this.notifications.newFeedback$.subscribe((p: NewFeedbackEvent) => {
          this.toasts.show({
            title: 'New feedback',
            message: `Created ${new Date(p.createdAtUtc).toLocaleString()}`,
            level: 'success',
            actions: [{ label: 'Open', run: () => this.router.navigate(['/feedbacks', p.feedbackId]) }],
            timeoutMs: 8000
          });
        })
      );

      this.subs.push(
        this.notifications.deleteRequestCreated$.subscribe((p: DeleteRequestCreatedEvent) => {
          this.toasts.show({
            title: 'Delete request',
            message: p.reason,
            level: 'warning',
            actions: [{ label: 'Review', run: () => this.router.navigate(['/admin/requests'], { queryParams: { selected: p.deleteRequestId, t: Date.now() } }) }],
            timeoutMs: 10000
          });
        })
      );
    }
  }

  ngOnDestroy(): void {
    this.subs.forEach(s => s?.unsubscribe?.());
  }
}
