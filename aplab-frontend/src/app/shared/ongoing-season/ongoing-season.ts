import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SeasonLike } from '../season-card/season-card';

@Component({
  selector: 'app-ongoing-season',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './ongoing-season.html',
  styleUrl: './ongoing-season.css'
})
export class OngoingSeason {
  @Input() title = 'Ongoing season';
  @Input() season: SeasonLike | null = null;
}
