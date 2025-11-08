import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { map, Observable } from 'rxjs';

export type Role = 'admin'|'mentor'|'intern'|'guest';
export type SortBy = 'createdAt'|'name'|'email';

export interface UserListItem {
  id: string;
  fullName?: string | null;
  email: string;
  role: Role;
  createdAtUtc?: string | null;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
}

export interface UsersQuery {
  page: number;
  pageSize: number;
  q?: string;
  role?: Role;
  from?: string;
  to?: string;
  sortBy?: SortBy;
  sortDir?: 'asc'|'desc';
}

export interface UserDto {
  id: string;
  fullName: string | null;
  email: string;
  roleName: string | null;
  keycloakId: string;
  seasonId: number | null;
  createdAtUtc?: string | null;
  desc?: string | null;
}

export interface CreateUserRequest {
  fullName: string;
  email: string;
  desc: string | null;
  seasonId: null;
  roleName: Role | null;
  password: string;
  forcePasswordChange: boolean;
}

@Injectable({ providedIn: 'root' })
export class Users {
  private http = inject(HttpClient);
  private base = '/api/users';

  getPaged(q: UsersQuery): Observable<PagedResult<UserListItem>> {
    let params = new HttpParams()
      .set('page', String(q.page))
      .set('pageSize', String(q.pageSize));
    if (q.q) params = params.set('q', q.q);
    if (q.role) params = params.set('role', q.role);
    if (q.from) params = params.set('from', q.from);
    if (q.to) params = params.set('to', q.to);
    if (q.sortBy) params = params.set('sortBy', q.sortBy);
    if (q.sortDir) params = params.set('sortDir', q.sortDir);
    return this.http.get<PagedResult<UserListItem>>(this.base, { params });
  }

  getMentors(limit: number = 200): Observable<UserListItem[]> {
    return this.getPaged({
      page: 1,
      pageSize: limit,
      role: 'mentor',
      sortBy: 'name',
      sortDir: 'asc'
    }).pipe(map(r => r.items));
  }

  create(req: CreateUserRequest): Observable<UserDto> {
    return this.http.post<UserDto>(this.base, req);
  }
}
