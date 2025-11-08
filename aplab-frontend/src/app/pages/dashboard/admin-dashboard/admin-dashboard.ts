import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Navbar } from '../../../shared/navbar/navbar';
import { SeasonList } from '../../../shared/season-list/season-list';
import { UsersTable } from '../../../shared/users-table/users-table';
import { Seasons, SeasonDto } from '../../../services/seasons';
import { Auth } from '../../../services/auth';

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, Navbar, SeasonList, UsersTable],
  templateUrl: './admin-dashboard.html',
  styleUrl: './admin-dashboard.css'
})
export class AdminDashboard implements OnInit {
  private router = inject(Router);
  private auth = inject(Auth);
  private seasonsApi = inject(Seasons);

  loadingSeasons = true;

  seasons: SeasonDto[] = [];
  filteredSeasons: SeasonDto[] = [];

  qSeason = '';
  seasonFrom = '';
  seasonTo = '';
  seasonSort: 'start-asc'|'start-desc' = 'start-asc';

  ngOnInit(): void {
    if (!this.auth.isLoggedIn()) { this.router.navigate(['/login']); return; }
    this.seasonsApi.getAll().subscribe({
      next: ss => { this.seasons = ss; this.loadingSeasons = false; this.applySeasonFilters(); },
      error: () => { this.seasons = []; this.loadingSeasons = false; this.applySeasonFilters(); }
    });
  }

  addSeason() { this.router.navigate(['/seasons/new']); }
  addUser() { this.router.navigate(['/users/new']); }
  openSeason(id: number) { this.router.navigate(['/seasons', id]); }
  openUser(id: string) { this.router.navigate(['/users', id]); }

  applySeasonFilters() {
    const from = this.seasonFrom ? new Date(this.seasonFrom) : null;
    const to = this.seasonTo ? new Date(this.seasonTo) : null;
    const q = this.qSeason.trim().toLowerCase();
    const arr = this.seasons.filter(s => {
      const name = (s.name || '').toLowerCase();
      if (q && !name.includes(q)) return false;
      const sd = s.startDate ? new Date(s.startDate) : null;
      const ed = s.endDate ? new Date(s.endDate) : null;
      if (from && sd && sd < from) return false;
      if (to && ed && ed > to) return false;
      return true;
    });
    arr.sort((a,b) => {
      const ad = new Date(a.startDate).getTime();
      const bd = new Date(b.startDate).getTime();
      return this.seasonSort === 'start-asc' ? ad - bd : bd - ad;
    });
    this.filteredSeasons = arr;
  }
}
