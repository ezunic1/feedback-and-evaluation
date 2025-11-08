import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface SeasonLike {
  id: number;
  name: string;
  startDate: string;
  endDate: string;
  mentorName?: string | null;
  usersCount?: number | null;
}

@Component({
  selector: 'app-season-card',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './season-card.html',
  styleUrl: './season-card.css'
})
export class SeasonCard {
  @Input() season!: SeasonLike;
  @Output() open = new EventEmitter<number>();
}
