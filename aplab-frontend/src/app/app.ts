import { Component, OnDestroy, OnInit, inject, signal, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { Router, RouterOutlet } from '@angular/router';
import { ToastContainer } from './shared/toast-container/toast-container';
import { Notifications } from './services/notifications';
import { Toasts } from './services/toasts';
import { NewFeedbackEvent, DeleteRequestCreatedEvent } from './models/notifications';
import { Auth } from './services/auth';

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
  private auth = inject(Auth);
  private subs: any[] = [];

  ngOnInit(): void {
    if (!isPlatformBrowser(this.platformId)) return;

    this.notifications.connectWhenAuthenticated('');

    this.subs.push(
      this.notifications.newFeedback$.subscribe((p: NewFeedbackEvent) => {
        const role = this.auth.role();
        const target =
          role === 'admin'  ? ['/admin/feedbacks',  p.feedbackId] :
          role === 'mentor' ? ['/mentor/feedbacks', p.feedbackId] :
                              ['/intern/feedbacks', p.feedbackId];

        this.toasts.show({
          title: 'New feedback',
          message: `Created ${new Date(p.createdAtUtc).toLocaleString()}`,
          level: 'success',
          actions: [{ label: 'Open', run: () => this.router.navigate(target) }],
          timeoutMs: 8000
        });
      })
    );

    this.subs.push(
      this.notifications.deleteRequestCreated$.subscribe((p: DeleteRequestCreatedEvent) => {
        const isAdmin = this.auth.hasRole('admin');
        this.toasts.show({
          title: 'Delete request',
          message: p.reason,
          level: 'warning',
          actions: isAdmin
            ? [{ label: 'Review', run: () => this.router.navigate(['/admin/requests'], { queryParams: { selected: p.deleteRequestId, t: Date.now() } }) }]
            : [], 
          timeoutMs: 8000
        });
      })
    );
  }

  ngOnDestroy(): void {
    this.subs.forEach(s => s?.unsubscribe?.());
  }
}
