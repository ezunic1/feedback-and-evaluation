import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FeedbackDto } from '../../services/feedbacks';
import { Role } from '../../services/users';

@Component({
  selector: 'app-feedback-card',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './feedback-card.html',
  styleUrls: ['./feedback-card.css']
})
export class FeedbackCard {
  @Input() feedback!: FeedbackDto;
  @Input() fromName = '';
  @Input() toName = '';
  @Input() fromRole?: Role | string | null;
  @Input() toRole?: Role | string | null;

  expanded = false;

  get hasGrades(): boolean {
    return !!this.feedback?.grade;
  }

  get createdAtLocal(): string {
    if (!this.feedback?.createdAtUtc) return '';
    return new Date(this.feedback.createdAtUtc).toLocaleString();
  }

  toggleExpanded(): void {
    this.expanded = !this.expanded;
  }
}
