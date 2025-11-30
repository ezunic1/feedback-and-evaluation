import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

export type ToastLevel = 'info'|'success'|'warning'|'danger';
export type ToastAction = { label: string; run: () => void };
export type Toast = {
  id: string;
  title: string;
  message?: string;
  level?: ToastLevel;
  actions?: ToastAction[];
  timeoutMs?: number;
};

@Injectable({ providedIn: 'root' })
export class Toasts {
  private list: Toast[] = [];
  private subj = new BehaviorSubject<Toast[]>([]);
  toasts$ = this.subj.asObservable();

  show(t: Omit<Toast,'id'>) {
    const id = crypto.randomUUID ? crypto.randomUUID() : Math.random().toString(36).slice(2);
    const toast: Toast = { id, level: 'info', timeoutMs: 7000, ...t };
    this.list = [toast, ...this.list];
    this.subj.next(this.list);
    if (toast.timeoutMs && toast.timeoutMs > 0) {
      setTimeout(() => this.dismiss(id), toast.timeoutMs);
    }
  }

  dismiss(id: string) {
    this.list = this.list.filter(t => t.id !== id);
    this.subj.next(this.list);
  }

  clear() {
    this.list = [];
    this.subj.next(this.list);
  }
}
