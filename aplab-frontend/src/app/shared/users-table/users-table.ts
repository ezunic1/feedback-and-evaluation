import { Component, EventEmitter, Input, OnInit, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Users, Role, PagedResult, UserListItem } from '../../services/users';
import { Seasons, SeasonDto } from '../../services/seasons';
import { Spinner } from '../../shared/spinner/spinner';

@Component({
  selector: 'app-users-table',
  standalone: true,
  imports: [CommonModule, FormsModule, Spinner],
  templateUrl: './users-table.html',
  styleUrl: './users-table.css'
})
export class UsersTable implements OnInit {
  private api = inject(Users);
  private seasonsApi = inject(Seasons);
  private router = inject(Router);

  @Input() initialPageSize = 10;
  @Input() showFilters = true;
  @Input() showPagination = true;
  @Output() open = new EventEmitter<string>();

  loading = true;
  rows: UserListItem[] = [];
  page = 1;
  pageSize = this.initialPageSize;
  total = 0;
  totalPages = 0;
  pages: number[] = [];

  sortBy: 'createdAt' | 'name' | 'email' = 'createdAt';
  sortDir: 'asc' | 'desc' = 'desc';

  role: 'all' | Role = 'all';
  seasons: SeasonDto[] = [];
  seasonsLoading = false;
  selectedSeason: number | 'all' = 'all';

  private debounceHandle: any;
  private rid = 0;

  ngOnInit(): void {
    this.pageSize = this.initialPageSize;
    if (this.role === 'intern') this.loadSeasons();
    this.load();
  }

  private loadSeasons() {
    if (this.seasons.length || this.seasonsLoading) return;
    this.seasonsLoading = true;
    this.seasonsApi.getAll().subscribe({
      next: s => { this.seasons = s; this.seasonsLoading = false; },
      error: () => { this.seasons = []; this.seasonsLoading = false; }
    });
  }

  load() {
    this.loading = true;
    const id = ++this.rid;
    this.api.getPaged({
      page: this.page,
      pageSize: this.pageSize,
      role: this.role !== 'all' ? this.role : undefined,
      sortBy: this.sortBy,
      sortDir: this.sortDir,
      seasonId: this.role === 'intern' && this.selectedSeason !== 'all' ? Number(this.selectedSeason) : undefined
    }).subscribe({
      next: (res: PagedResult<UserListItem>) => {
        if (id !== this.rid) return;
        this.rows = res.items.map(i => ({
          ...i,
          createdAtUtc: i.createdAtUtc ?? (i as any).createdAt ?? null
        }));
        this.page = res.page;
        this.pageSize = res.pageSize;
        this.total = res.total;
        this.totalPages = res.totalPages;
        this.pages = Array.from({ length: this.totalPages }, (_, i) => i + 1);
        this.loading = false;
      },
      error: () => {
        if (id !== this.rid) return;
        this.rows = [];
        this.total = 0;
        this.totalPages = 0;
        this.pages = [];
        this.loading = false;
      }
    });
  }

  onRoleChanged() {
    if (this.role === 'intern') {
      this.selectedSeason = 'all';
      this.loadSeasons();
    } else {
      this.selectedSeason = 'all';
    }
    this.page = 1;
    clearTimeout(this.debounceHandle);
    this.debounceHandle = setTimeout(() => this.load(), 200);
  }

  onSeasonChanged() {
    this.page = 1;
    clearTimeout(this.debounceHandle);
    this.debounceHandle = setTimeout(() => this.load(), 200);
  }

  changeSort(field: 'createdAt' | 'name' | 'email') {
    if (this.sortBy === field) {
      this.sortDir = this.sortDir === 'asc' ? 'desc' : 'asc';
    } else {
      this.sortBy = field;
      this.sortDir = field === 'createdAt' ? 'desc' : 'asc';
    }
    this.page = 1;
    this.load();
  }

  pageClick(p: number) {
    if (p !== this.page) {
      this.page = p;
      this.load();
    }
  }

  openRow(id: string) {
    this.router.navigate(['/users', id]);
  }
}
