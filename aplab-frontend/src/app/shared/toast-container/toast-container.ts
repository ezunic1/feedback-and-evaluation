import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Toasts, Toast } from '../../services/toasts';
import { Router } from '@angular/router';

@Component({
  selector: 'app-toast-container',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './toast-container.html',
  styleUrls: ['./toast-container.css']
})
export class ToastContainer {
  toasts$ = inject(Toasts).toasts$;
  private toasts = inject(Toasts);
  private router = inject(Router);

  dismiss(id: string) { this.toasts.dismiss(id); }

  onToastClick(t: Toast) {
    const first = t.actions && t.actions.length ? t.actions[0] : null;
    if (first && typeof first.run === 'function') first.run();
  }
}
