import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { Navbar } from '../../../shared/navbar/navbar';
import { SeasonList } from '../../../shared/season-list/season-list';
import { Seasons, SeasonDto, UserDto } from '../../../services/seasons';
import { Auth } from '../../../services/auth';

@Component({
  selector: 'app-intern-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, Navbar,  SeasonList],
  templateUrl: './intern-dashboard.html',
  styleUrl: './intern-dashboard.css'
})
export class InternDashboard implements OnInit {
  private router = inject(Router);
  private auth = inject(Auth);
  private seasonsApi = inject(Seasons);

  loading = true;
  mySeason: SeasonDto | null = null;
  others: SeasonDto[] = [];
  peers: UserDto[] = [];

  ngOnInit(): void {
    if (!this.auth.isLoggedIn()) { this.router.navigate(['/login']); return; }

    this.seasonsApi.getMySeason().subscribe({
      next: s => {
        this.mySeason = s ?? null;
        if (this.mySeason) {
          this.seasonsApi.getMySeasonUsers().subscribe({
            next: u => { this.peers = u; this.loadOthers(); },
            error: () => { this.peers = []; this.loadOthers(); }
          });
        } else {
          this.loadOthers();
        }
      },
      error: () => { this.mySeason = null; this.loadOthers(); }
    });
  }

  private loadOthers() {
    this.seasonsApi.getAll().subscribe({
      next: ss => {
        const mineId = this.mySeason?.id ?? -1;
        this.others = (ss || []).filter(s => s.id !== mineId);
        this.loading = false;
      },
      error: () => { this.others = []; this.loading = false; }
    });
  }

  openSeason(id: number) { this.router.navigate(['/seasons', id]); }
}
