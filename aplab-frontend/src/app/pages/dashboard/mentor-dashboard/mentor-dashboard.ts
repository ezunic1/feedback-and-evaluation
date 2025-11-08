import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { Navbar } from '../../../shared/navbar/navbar';
import { OngoingSeason } from '../../../shared/ongoing-season/ongoing-season';
import { MySeasonsList } from '../../../shared/my-seasons-list/my-seasons-list';
import { SeasonList } from '../../../shared/season-list/season-list';
import { Seasons, SeasonDto } from '../../../services/seasons';
import { Auth } from '../../../services/auth';

@Component({
  selector: 'app-mentor-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, Navbar, OngoingSeason, MySeasonsList, SeasonList],
  templateUrl: './mentor-dashboard.html',
  styleUrl: './mentor-dashboard.css'
})
export class MentorDashboard implements OnInit {
  private router = inject(Router);
  private auth = inject(Auth);
  private seasonsApi = inject(Seasons);

  loading = true;
  all: SeasonDto[] = [];
  ongoing: SeasonDto | null = null;
  myPrevious: SeasonDto[] = [];
  others: SeasonDto[] = [];

  ngOnInit(): void {
    if (!this.auth.isLoggedIn()) { this.router.navigate(['/login']); return; }
    const meName = (this.auth.user()?.name || '').toLowerCase();
    const now = new Date().getTime();

    this.seasonsApi.getAll().subscribe({
      next: ss => {
        this.all = ss || [];
        const mine = this.all.filter(s => (s.mentorName || '').toLowerCase() === meName);
        const notMine = this.all.filter(s => (s.mentorName || '').toLowerCase() !== meName);

        this.ongoing = mine.find(s => {
          const sd = new Date(s.startDate).getTime();
          const ed = new Date(s.endDate).getTime();
          return sd <= now && now <= ed;
        }) || null;

        this.myPrevious = mine
          .filter(s => new Date(s.endDate).getTime() < now)
          .sort((a,b) => new Date(b.endDate).getTime() - new Date(a.endDate).getTime());

        this.others = notMine.sort((a,b) => new Date(a.startDate).getTime() - new Date(b.startDate).getTime());
        this.loading = false;
      },
      error: () => { this.loading = false; }
    });
  }

  openSeason(id: number) { this.router.navigate(['/seasons', id]); }
}
