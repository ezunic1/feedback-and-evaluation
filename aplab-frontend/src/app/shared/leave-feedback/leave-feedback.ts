import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Feedbacks, CreateInternFeedbackRequest, CreateMentorFeedbackRequest } from '../../services/feedbacks';
import { UserDto as SeasonUserDto } from '../../services/seasons';
import { Spinner } from '../spinner/spinner';

@Component({
  selector: 'app-leave-feedback',
  standalone: true,
  imports: [CommonModule, FormsModule, Spinner],
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
  errorMessage = '';

  get isMentorForm(): boolean {
    return this.currentRole === 'mentor';
  }

  onSubmit(): void {
    if (this.saving) return;
    this.errorMessage = '';

    if (this.isMentorForm) {
      if (this.careerRating == null || this.communicationRating == null || this.collaborationRating == null) {
        this.errorMessage = 'Please select all ratings.';
        return;
      }

      const body: CreateMentorFeedbackRequest = {
        receiverUserId: this.targetUser.id,
        comment: this.comment.trim(),
        careerSkills: this.careerRating as number,
        communication: this.communicationRating as number,
        collaboration: this.collaborationRating as number
      };

      this.saving = true;
      this.api.createAsMentor(body).subscribe({
        next: () => {
          this.saving = false;
          this.close.emit(true);
        },
        error: () => {
          this.saving = false;
          this.errorMessage = 'Failed to submit feedback. Please try again.';
        }
      });
    } else {
      const body: CreateInternFeedbackRequest = {
        receiverUserId: this.targetUser.id,
        comment: this.comment.trim()
      };

      this.saving = true;
      this.api.createAsIntern(body).subscribe({
        next: () => {
          this.saving = false;
          this.close.emit(true);
        },
        error: () => {
          this.saving = false;
          this.errorMessage = 'Failed to submit feedback. Please try again.';
        }
      });
    }
  }

  onCancel(): void {
    if (this.saving) return;
    this.close.emit(false);
  }
}
