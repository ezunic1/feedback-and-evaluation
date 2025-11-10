import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { Navbar } from '../../shared/navbar/navbar';
import { Seasons, SeasonDto, UserDto as SeasonUserDto } from '../../services/seasons';
import { Users, UserListItem, UpdateUserRequest, Role, UserDto as UsersUserDto } from '../../services/users';
import { Auth } from '../../services/auth';
import { forkJoin } from 'rxjs';

@Component({
  selector: 'app-season',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule, Navbar],
  templateUrl: './season.html',
  styleUrl: './season.css'
})
export class SeasonPage implements OnInit {
  private seasonsApi = inject(Seasons);
  private usersApi = inject(Users);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private auth = inject(Auth);

  id = 0;
  loading = true;
  season: SeasonDto | null = null;
  users: SeasonUserDto[] = [];

  isAdmin = false;
  isMentor = false;
  canEditMentor = false;
  canEditName = false;
  canDelete = false;
  canManageInterns = false;

  isEditingName = false;
  nameInput = '';
  savingName = false;

  isEditingMentor = false;
  mentorInput: string | null = null;
  mentors: Array<{ id: string; fullName: string }> = [];
  savingMentor = false;

  showAddModal = false;
  addTab: 'interns' | 'guests' = 'interns';
  internCandidates: UserListItem[] = [];
  guestCandidates: UserListItem[] = [];
  candidateQuery = '';
  addingUserId: string | null = null;

  deleting = false;

  ngOnInit(): void {
    if (!this.auth.isLoggedIn()) { this.router.navigate(['/login']); return; }
    const roles = this.auth.roles().map(r => r.toLowerCase());
    this.isAdmin = roles.includes('admin');
    this.isMentor = roles.includes('mentor');
    const showAdminUi = this.isAdmin && !this.isMentor;
    this.canEditMentor = showAdminUi;
    this.canEditName = showAdminUi;
    this.canDelete = showAdminUi;
    this.canManageInterns = this.isAdmin || this.isMentor;
    this.id = Number(this.route.snapshot.paramMap.get('id'));
    if (!this.id) { this.router.navigate(['/dashboard']); return; }
    this.loadSeason();
  }

  private loadSeason(): void {
    this.loading = true;
    this.seasonsApi.getById(this.id).subscribe({
      next: s => {
        this.season = s;
        this.nameInput = s?.name ?? '';
        this.mentorInput = s?.mentorId ?? null;
        this.loadUsers();
      },
      error: () => { this.season = null; this.loading = false; }
    });
  }

  private loadUsers(): void {
    this.seasonsApi.getUsers(this.id).subscribe({
      next: us => { this.users = us; this.loading = false; },
      error: () => {
        this.usersApi.getBySeason(this.id).subscribe({
          next: list => {
            this.users = list.map(x => ({
              id: x.id,
              fullName: x.fullName ?? '',
              email: x.email,
              roleName: x.role,
              keycloakId: '',
              seasonId: this.id
            }));
            this.loading = false;
          },
          error: () => { this.users = []; this.loading = false; }
        });
      }
    });
  }

  startEditName(): void {
    if (!this.season || !this.canEditName) return;
    this.isEditingName = true;
    this.nameInput = this.season.name;
  }

  cancelEditName(): void {
    if (!this.season) return;
    this.isEditingName = false;
    this.nameInput = this.season.name;
  }

  saveName(): void {
    if (!this.season || !this.canEditName) return;
    const name = (this.nameInput || '').trim();
    if (!name) return;
    this.savingName = true;
    const body = {
      name,
      startDate: this.season.startDate,
      endDate: this.season.endDate,
      mentorId: this.season.mentorId ?? null
    };
    this.seasonsApi.update(this.season.id, body).subscribe({
      next: s => {
        this.season = s;
        this.isEditingName = false;
        this.savingName = false;
        this.mentorInput = this.season?.mentorId ?? null;
      },
      error: () => { this.savingName = false; }
    });
  }

  startEditMentor(): void {
    if (!this.canEditMentor) return;
    this.isEditingMentor = true;
    this.loadMentors();
  }

  cancelEditMentor(): void {
    if (!this.season) return;
    this.isEditingMentor = false;
    this.mentorInput = this.season.mentorId ?? null;
  }

  private loadMentors(): void {
    this.usersApi.getMentors(200).subscribe({
      next: list => { this.mentors = list.map(x => ({ id: x.id, fullName: x.fullName || x.email || '—' })); },
      error: () => { this.mentors = []; }
    });
  }

  saveMentor(): void {
    if (!this.season || !this.canEditMentor) return;
    this.savingMentor = true;
    this.seasonsApi.assignMentor(this.season.id, this.mentorInput).subscribe({
      next: () => {
        this.seasonsApi.getById(this.season!.id).subscribe({
          next: s => { this.season = s; this.isEditingMentor = false; this.savingMentor = false; },
          error: () => { this.isEditingMentor = false; this.savingMentor = false; }
        });
      },
      error: () => { this.savingMentor = false; }
    });
  }

  deleteSeason(): void {
    if (!this.season || !this.canDelete || this.deleting) return;
    const ok = confirm('Delete this season? This cannot be undone.');
    if (!ok) return;
    this.deleting = true;
    this.seasonsApi.delete(this.season.id).subscribe({
      next: () => { this.deleting = false; this.router.navigate(['/dashboard']); },
      error: () => { this.deleting = false; }
    });
  }

  openUser(id: string): void {
    this.router.navigate(['/users', id]);
  }

  removeIntern(userId: string): void {
    if (!this.season || !this.canManageInterns) return;
    const call$ = this.isAdmin
      ? this.seasonsApi.removeUser(this.season.id, userId)
      : this.seasonsApi.mentorRemoveUser(this.season.id, userId);
    call$.subscribe({ next: () => this.loadUsers(), error: () => {} });
  }

  openAddIntern(): void {
    if (!this.canManageInterns) return;
    this.showAddModal = true;
    this.addTab = 'interns';
    this.candidateQuery = '';
    this.loadCandidates();
  }

  closeAddIntern(): void {
    this.showAddModal = false;
    this.addingUserId = null;
  }

  private loadCandidates(): void {
    this.loadInternCandidates();
    this.usersApi.getPaged({ page: 1, pageSize: 400, role: 'guest', sortBy: 'name', sortDir: 'asc' }).subscribe({
      next: res => { this.guestCandidates = res.items || []; },
      error: () => { this.guestCandidates = []; }
    });
  }

  private loadInternCandidates(): void {
    this.usersApi.getPaged({ page: 1, pageSize: 400, role: 'intern', sortBy: 'name', sortDir: 'asc' }).subscribe({
      next: res => {
        const items = res.items || [];
        if (!items.length) { this.internCandidates = []; return; }
        const calls = items.map(i => this.usersApi.getById(i.id));
        forkJoin(calls).subscribe({
          next: (dtos: UsersUserDto[]) => {
            const free = dtos.filter(d => d.seasonId == null);
            this.internCandidates = free.map(d => ({
              id: d.id,
              fullName: d.fullName || null,
              email: d.email,
              role: ((d.roleName || 'intern') as Role),
              createdAtUtc: d.createdAtUtc || null
            }));
          },
          error: () => { this.internCandidates = []; }
        });
      },
      error: () => { this.internCandidates = []; }
    });
  }

  filteredCandidates(list: UserListItem[]): UserListItem[] {
    const q = (this.candidateQuery || '').toLowerCase().trim();
    if (!q) return list;
    return list.filter(x => {
      const n = (x.fullName || '').toLowerCase();
      const e = (x.email || '').toLowerCase();
      return n.includes(q) || e.includes(q);
    });
  }

  addInternFromList(u: UserListItem): void {
    if (!this.season || !this.canManageInterns) return;
    this.addingUserId = u.id;
    const call$ = this.isAdmin
      ? this.seasonsApi.addUser(this.season.id, u.id)
      : this.seasonsApi.mentorAddUser(this.season.id, u.id);
    call$.subscribe({
      next: () => {
        this.addingUserId = null;
        this.loadUsers();
        this.loadInternCandidates();
      },
      error: () => { this.addingUserId = null; }
    });
  }

  addGuestFromList(u: UserListItem): void {
    if (!this.season || !this.canManageInterns) return;
    this.addingUserId = u.id;
    if (this.isAdmin) {
      const body: UpdateUserRequest = { fullName: u.fullName || '', desc: null, roleName: 'intern', seasonId: null };
      this.usersApi.update(u.id, body).subscribe({
        next: () => {
          this.seasonsApi.addUser(this.season!.id, u.id).subscribe({
            next: () => {
              this.addingUserId = null;
              this.loadUsers();
              this.guestCandidates = this.guestCandidates.filter(x => x.id !== u.id);
            },
            error: () => { this.addingUserId = null; }
          });
        },
        error: () => { this.addingUserId = null; }
      });
    } else {
      this.seasonsApi.mentorAddUser(this.season.id, u.id).subscribe({
        next: () => {
          this.addingUserId = null;
          this.loadUsers();
          this.guestCandidates = this.guestCandidates.filter(x => x.id !== u.id);
        },
        error: () => { this.addingUserId = null; }
      });
    }
  }
}
