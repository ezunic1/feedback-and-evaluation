import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, ActivatedRoute, Router } from '@angular/router';
import { Seasons, SeasonDto, UserDto } from '../../services/seasons';
import { Auth } from '../../services/auth';

@Component({
  selector: 'app-season',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './season.html',
  styleUrl: './season.css'
})
export class SeasonPage implements OnInit {
  private seasonsApi = inject(Seasons);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private auth = inject(Auth);

  id = 0;
  loading = true;
  season: SeasonDto | null = null;
  users: UserDto[] = [];

  ngOnInit(): void {
    if (!this.auth.isLoggedIn()) { this.router.navigate(['/login']); return; }
    this.id = Number(this.route.snapshot.paramMap.get('id'));
    if (!this.id) { this.router.navigate(['/dashboard']); return; }

    this.seasonsApi.getById(this.id).subscribe({
      next: s => { this.season = s; this.loadUsers(); },
      error: () => { this.season = null; this.loading = false; }
    });
  }

  private loadUsers(): void {
    this.seasonsApi.getUsers(this.id).subscribe({
      next: us => { this.users = us; this.loading = false; },
      error: () => { this.users = []; this.loading = false; }
    });
  }
}
