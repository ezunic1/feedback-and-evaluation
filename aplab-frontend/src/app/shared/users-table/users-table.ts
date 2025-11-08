import { Component, EventEmitter, Input, OnInit, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Users, Role, PagedResult, UserListItem } from '../../services/users';

@Component({
  selector: 'app-users-table',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './users-table.html',
  styleUrl: './users-table.css'
})
export class UsersTable implements OnInit {
  private api = inject(Users);

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

  sortBy: 'createdAt'|'name'|'email' = 'createdAt';
  sortDir: 'asc'|'desc' = 'desc';

  q = '';
  role: 'all'|Role = 'all';
  from = '';
  to = '';

  private debounceHandle: any;

  ngOnInit(): void {
    this.pageSize = this.initialPageSize;
    this.load();
  }

  load() {
    this.loading = true;
    this.api.getPaged({
      page: this.page,
      pageSize: this.pageSize,
      q: this.q || undefined,
      role: this.role !== 'all' ? this.role : undefined,
      from: this.from || undefined,
      to: this.to || undefined,
      sortBy: this.sortBy,
      sortDir: this.sortDir
    }).subscribe({
      next: (res: PagedResult<UserListItem>) => {
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
        this.rows = [];
        this.total = 0;
        this.totalPages = 0;
        this.pages = [];
        this.loading = false;
      }
    });
  }

  onFiltersChanged() {
    this.page = 1;
    clearTimeout(this.debounceHandle);
    this.debounceHandle = setTimeout(() => this.load(), 250);
  }

  changeSort(field: 'createdAt'|'name'|'email') {
    if (this.sortBy === field) {
      this.sortDir = this.sortDir === 'asc' ? 'desc' : 'asc';
    } else {
      this.sortBy = field;
      this.sortDir = field === 'createdAt' ? 'desc' : 'asc';
    }
    this.load();
  }

  pageClick(p: number) {
    if (p !== this.page) {
      this.page = p;
      this.load();
    }
  }

  openRow(id: string) {
    this.open.emit(id);
  }
}
