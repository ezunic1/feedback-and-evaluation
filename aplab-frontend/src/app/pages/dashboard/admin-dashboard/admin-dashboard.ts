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
    const q = this.qSeason.trim().toLowerCase();
    this.filteredSeasons = this.seasons.filter(s => {
      const name = (s.name || '').toLowerCase();
      return q ? name.includes(q) : true;
    });
  }
}
