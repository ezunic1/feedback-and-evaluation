import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SeasonCard, SeasonLike } from '../season-card/season-card';

@Component({
  selector: 'app-season-list',
  standalone: true,
  imports: [CommonModule, SeasonCard],
  templateUrl: './season-list.html',
  styleUrl: './season-list.css'
})
export class SeasonList {
  @Input() seasons: SeasonLike[] = [];
  @Output() open = new EventEmitter<number>();
}
