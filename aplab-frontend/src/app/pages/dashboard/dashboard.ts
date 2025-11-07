import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { Seasons, SeasonDto, UserDto } from '../../services/seasons';
import { Auth } from '../../services/auth';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css'
})
export class Dashboard implements OnInit {
  private seasonsApi = inject(Seasons);
  private router = inject(Router);
  public auth = inject(Auth);

  role = 'guest';
  loading = true;

  seasons: SeasonDto[] = [];
  mySeason: SeasonDto | null = null;
  peers: UserDto[] = [];

  ngOnInit(): void {
    if (!this.auth.isLoggedIn()) {
      this.router.navigate(['/login']);
      return;
    }
    this.role = (this.auth.role() || 'guest').toLowerCase();

    if (this.role === 'intern') {
      this.seasonsApi.getMySeason().subscribe({
        next: s => {
          this.mySeason = s;
          if (s) {
            this.seasonsApi.getMySeasonUsers().subscribe({
              next: u => { this.peers = u; this.loading = false; },
              error: () => { this.peers = []; this.loading = false; }
            });
          } else {
            this.loading = false;
          }
        },
        error: () => { this.mySeason = null; this.loading = false; }
      });
    } else if (this.role === 'mentor' || this.role === 'admin') {
      this.seasonsApi.getAll().subscribe({
        next: ss => { this.seasons = ss; this.loading = false; },
        error: () => { this.seasons = []; this.loading = false; }
      });
    } else {
      this.loading = false;
    }
  }

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/']);
  }
}
