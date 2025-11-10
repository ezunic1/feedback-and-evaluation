import { Component, EventEmitter, Input, OnChanges, OnInit, Output, SimpleChanges, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Seasons } from '../../services/seasons';

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
export class SeasonCard implements OnInit, OnChanges {
  @Input() season!: SeasonLike;
  @Output() open = new EventEmitter<number>();

  private seasonsApi = inject(Seasons);
  displayCount: number | null = null;
  private lastFetchedForId: number | null = null;

  ngOnInit(): void {
    this.refreshCountIfNeeded();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['season']) this.refreshCountIfNeeded();
  }

  private refreshCountIfNeeded(): void {
    if (!this.season || !this.season.id) return;
    const hasCount = typeof this.season.usersCount === 'number' && this.season.usersCount! > 0;
    if (hasCount) {
      this.displayCount = this.season.usersCount as number;
      this.lastFetchedForId = this.season.id;
      return;
    }
    if (this.lastFetchedForId === this.season.id) return;
    this.lastFetchedForId = this.season.id;
    this.seasonsApi.getUsers(this.season.id).subscribe({
      next: users => { this.displayCount = users.length; },
      error: () => { this.displayCount = this.season.usersCount ?? null; }
    });
  }
}
