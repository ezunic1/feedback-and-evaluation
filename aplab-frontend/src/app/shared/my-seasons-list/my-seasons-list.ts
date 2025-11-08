import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SeasonList} from '../season-list/season-list';
import { SeasonLike } from '../season-card/season-card';

@Component({
  selector: 'app-my-seasons-list',
  standalone: true,
  imports: [CommonModule, SeasonList],
  templateUrl: './my-seasons-list.html',
  styleUrl: './my-seasons-list.css'
})
export class MySeasonsList {
  @Input() title = 'My previous seasons';
  @Input() seasons: SeasonLike[] = [];
  @Output() open = new EventEmitter<number>();
}
