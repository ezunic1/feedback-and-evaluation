import { Component, Input, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FeedbackDto, Feedbacks } from '../../services/feedbacks';
import { Role } from '../../services/users';
import { Auth } from '../../services/auth';
import { RequestDelete } from '../../shared/request-delete/request-delete';
import { ConfirmDelete } from '../../shared/confirm-delete/confirm-delete';

@Component({
  selector: 'app-feedback-card',
  standalone: true,
  imports: [CommonModule, RequestDelete, ConfirmDelete],
  templateUrl: './feedback-card.html',
  styleUrls: ['./feedback-card.css']
})
export class FeedbackCard {
  @Input() feedback!: FeedbackDto;
  @Input() fromName = '';
  @Input() toName = '';
  @Input() fromRole?: Role | string | null;
  @Input() toRole?: Role | string | null;

  private auth = inject(Auth);
  private feedbacksApi = inject(Feedbacks);

  expanded = false;
  requestOpen = false;
  deleting = false;
  deleted = false;

  confirmOpen = false;
  confirmLoading = false;
  confirmTitle = 'Delete feedback';
  confirmMessage = 'Are you sure you want to permanently delete this feedback?';
  confirmLabel = 'Delete';
  cancelLabel = 'Cancel';

  get hasGrades(): boolean {
    return !!this.feedback?.grade;
  }

  get createdAtLocal(): string {
    if (!this.feedback?.createdAtUtc) return '';
    return new Date(this.feedback.createdAtUtc).toLocaleString();
  }

  get showRequestDelete(): boolean {
    if (this.auth.hasRole('admin')) return false;
    return this.auth.hasRole('mentor') || this.auth.hasRole('intern');
  }

  get showDelete(): boolean {
    return this.auth.hasRole('admin');
  }

  toggleExpanded(): void {
    this.expanded = !this.expanded;
  }

  onRequestDeleteClick(ev: Event) {
    ev.stopPropagation();
    this.requestOpen = true;
  }

  onRequestClosed(submitted: boolean) {
    this.requestOpen = false;
    if (submitted) {
    }
  }

  onDeleteClick(ev: Event) {
    ev.stopPropagation();
    if (!this.feedback || this.deleting || this.deleted) return;
    this.confirmOpen = true;
    this.confirmLoading = false;
  }

  onCancelDelete() {
    if (this.confirmLoading) return;
    this.confirmOpen = false;
  }

  onConfirmDelete() {
    if (!this.feedback || this.confirmLoading) return;
    this.confirmLoading = true;
    this.deleting = true;

    this.feedbacksApi.delete(this.feedback.id).subscribe({
      next: () => {
        this.confirmLoading = false;
        this.deleting = false;
        this.confirmOpen = false;
        this.deleted = true;
      },
      error: () => {
        this.confirmLoading = false;
        this.deleting = false;
        alert('Failed to delete feedback.');
      }
    });
  }
}
