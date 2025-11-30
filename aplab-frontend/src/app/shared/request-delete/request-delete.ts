import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DeleteRequests } from '../../services/delete-requests';
import { ConfirmCreate } from '../confirm-create/confirm-create';

@Component({
  selector: 'app-request-delete',
  standalone: true,
  imports: [CommonModule, FormsModule, ConfirmCreate],
  templateUrl: './request-delete.html',
  styleUrl: './request-delete.css',
})
export class RequestDelete {
  @Input() open = false;
  @Input() feedbackId!: number;
  @Output() closed = new EventEmitter<boolean>();

  private api = inject(DeleteRequests);

  reason = '';
  saving = false;
  errorMessage = '';
  showConfirm = false;

  onCancel() {
    if (this.saving) return;
    this.open = false;
    this.reason = '';
    this.errorMessage = '';
    this.showConfirm = false;
    this.closed.emit(false);
  }

  onSubmit() {
    if (!this.reason.trim() || !this.feedbackId || this.saving) return;
    this.showConfirm = true;
  }

  onCancelConfirm() {
    this.showConfirm = false;
  }

  onConfirmCreate() {
    if (!this.feedbackId || !this.reason.trim()) return;
    this.saving = true;
    this.errorMessage = '';
    this.api.create({ feedbackId: this.feedbackId, reason: this.reason.trim() })
      .subscribe({
        next: () => {
          this.saving = false;
          this.showConfirm = false;
          this.open = false;
          this.closed.emit(true);
          this.reason = '';
        },
        error: (e) => {
          this.saving = false;
          this.errorMessage = e?.error?.title || e?.message || 'Failed';
        }
      });
  }
}
