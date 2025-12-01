import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Feedbacks, CreateInternFeedbackRequest, CreateMentorFeedbackRequest } from '../../services/feedbacks';
import { UserDto as SeasonUserDto } from '../../services/seasons';
import { Spinner } from '../spinner/spinner';
import { ConfirmCreate } from '../confirm-create/confirm-create';
import { ServerErrors } from '../server-errors/server-errors';
import { ProblemDetails } from '../../models/problem-details';

@Component({
  selector: 'app-leave-feedback',
  standalone: true,
  imports: [CommonModule, FormsModule, Spinner, ConfirmCreate, ServerErrors],
  templateUrl: './leave-feedback.html',
  styleUrls: ['./leave-feedback.css']
})
export class LeaveFeedback {
  private api = inject(Feedbacks);

  @Input() seasonId!: number;
  @Input() targetUser!: SeasonUserDto;
  @Input() currentRole!: 'admin' | 'mentor' | 'intern' | 'guest';

  @Output() close = new EventEmitter<boolean>();

  comment = '';
  careerRating: number | null = null;
  communicationRating: number | null = null;
  collaborationRating: number | null = null;

  saving = false;
  showConfirm = false;
  problem: ProblemDetails | null = null;

  get isMentorForm(): boolean {
    return this.currentRole === 'mentor';
  }

  onSubmit(): void {
    if (this.saving) return;
    this.showConfirm = true;
  }

  onConfirmCreate(): void {
    if (this.saving) return;
    this.showConfirm = false;
    this.saving = true;
    this.problem = null;

    if (this.isMentorForm) {
      const body: CreateMentorFeedbackRequest = {
        receiverUserId: this.targetUser.id,
        comment: this.comment.trim(),
        careerSkills: Number(this.careerRating ?? 0),
        communication: Number(this.communicationRating ?? 0),
        collaboration: Number(this.collaborationRating ?? 0)
      };
      this.api.createAsMentor(body).subscribe({
        next: () => { this.saving = false; this.close.emit(true); },
        error: (err: ProblemDetails) => { this.saving = false; this.problem = err; }
      });
    } else {
      const body: CreateInternFeedbackRequest = {
        receiverUserId: this.targetUser.id,
        comment: this.comment.trim()
      };
      this.api.createAsIntern(body).subscribe({
        next: () => { this.saving = false; this.close.emit(true); },
        error: (err: ProblemDetails) => { this.saving = false; this.problem = err; }
      });
    }
  }

  onCancelConfirm(): void {
    if (this.saving) return;
    this.showConfirm = false;
  }

  onCancel(): void {
    if (this.saving) return;
    this.close.emit(false);
  }
}
