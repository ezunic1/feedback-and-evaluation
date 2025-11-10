import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Users, UserDto, UpdateUserRequest, Role } from '../../services/users';
import { Seasons, SeasonDto } from '../../services/seasons';

@Component({
  selector: 'app-profile-view',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './profile-view.html',
  styleUrls: ['./profile-view.css']
})
export class ProfileView implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private api = inject(Users);
  private seasonsApi = inject(Seasons);

  id = '';
  loading = true;
  saving = false;
  deleting = false;

  user: UserDto | null = null;

  isEditing = false;
  fullNameInput = '';
  descInput: string | null = null;
  roleInput: Role = 'guest';
  seasonInput: number | null = null;

  seasons: SeasonDto[] = [];
  seasonsLoading = false;

  ngOnInit(): void {
    this.id = String(this.route.snapshot.paramMap.get('id') || '');
    if (!this.id) {
      this.router.navigate(['/users']);
      return;
    }
    this.load();
  }

  private loadSeasons(): void {
    if (this.seasons.length || this.seasonsLoading) return;
    this.seasonsLoading = true;
    this.seasonsApi.getAll().subscribe({
      next: s => { this.seasons = s; this.seasonsLoading = false; },
      error: () => { this.seasons = []; this.seasonsLoading = false; }
    });
  }

  private normalizeRole(r?: string | null): Role {
    const x = (r || '').toLowerCase();
    if (x.includes('admin')) return 'admin';
    if (x.includes('mentor')) return 'mentor';
    if (x.includes('intern')) return 'intern';
    if (x.includes('guest')) return 'guest';
    return 'guest';
  }

  load(): void {
    this.loading = true;
    this.api.getById(this.id).subscribe({
      next: u => {
        this.user = u;
        const role = this.normalizeRole(u.roleName);
        this.fullNameInput = u.fullName || '';
        this.descInput = u.desc ?? null;
        this.roleInput = role;
        this.seasonInput = u.seasonId ?? null;
        if (role === 'intern') this.loadSeasons();
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.router.navigate(['/users']);
      }
    });
  }

  edit(): void {
    if (!this.user) return;
    this.isEditing = true;
  }

  cancel(): void {
    if (!this.user) return;
    this.isEditing = false;
    this.fullNameInput = this.user.fullName || '';
    this.descInput = this.user.desc ?? null;
    this.roleInput = this.normalizeRole(this.user.roleName);
    this.seasonInput = this.user.seasonId ?? null;
  }

  onRoleChanged(): void {
    if (this.roleInput === 'intern') {
      this.loadSeasons();
    } else {
      this.seasonInput = null;
    }
  }

  async save(): Promise<void> {
    if (!this.user) return;
    this.saving = true;
    const body: UpdateUserRequest = {
      fullName: this.fullNameInput?.trim() || this.user.fullName || '',
      desc: this.descInput ?? null,
      roleName: this.roleInput,
      seasonId: this.roleInput === 'intern' ? (this.seasonInput ?? null) : null
    };
    this.api.update(this.user.id, body).subscribe({
      next: updated => {
        this.user = updated;
        this.isEditing = false;
        this.saving = false;
      },
      error: () => {
        this.saving = false;
      }
    });
  }

  deleteUser(): void {
    if (!this.user || this.deleting) return;
    const ok = confirm('Delete this user? This cannot be undone.');
    if (!ok) return;
    this.deleting = true;
    this.api.delete(this.user.id).subscribe({
      next: () => {
        this.deleting = false;
        this.router.navigate(['/users']);
      },
      error: () => {
        this.deleting = false;
      }
    });
  }
}
