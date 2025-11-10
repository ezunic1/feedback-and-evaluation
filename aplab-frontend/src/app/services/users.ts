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
  role?: Role;
  sortBy?: SortBy;
  sortDir?: 'asc'|'desc';
  seasonId?: number;
}

export interface UserDto {
  id: string;
  fullName: string | null;
  email: string;
  roleName: string | null;
  keycloakId: string;
  seasonId: number | null;
  seasonName?: string | null;
  createdAtUtc?: string | null;
  desc?: string | null;
}

export interface CreateUserRequest {
  fullName: string;
  email: string;
  desc: string | null;
  seasonId: number | null;
  roleName: Role | null;
  password: string;
  forcePasswordChange: boolean;
}

export interface UpdateUserRequest {
  fullName?: string | null;
  email?: string | null;
  desc?: string | null;
  roleName?: Role | null;
  seasonId?: number | null;
}

@Injectable({ providedIn: 'root' })
export class Users {
  private http = inject(HttpClient);
  private base = '/api/users';

  getPaged(q: UsersQuery): Observable<PagedResult<UserListItem>> {
    let params = new HttpParams()
      .set('page', String(q.page))
      .set('pageSize', String(q.pageSize));
    if (q.role) params = params.set('role', q.role);
    if (q.sortBy) params = params.set('sortBy', q.sortBy);
    if (q.sortDir) params = params.set('sortDir', q.sortDir);
    if (typeof q.seasonId === 'number') params = params.set('seasonId', String(q.seasonId));
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

  getBySeason(seasonId: number): Observable<UserListItem[]> {
    return this.http.get<UserListItem[]>(`${this.base}/by-season/${seasonId}`);
  }

  getById(id: string): Observable<UserDto> {
    return this.http.get<UserDto>(`${this.base}/${id}`);
  }

  create(req: CreateUserRequest): Observable<UserDto> {
    return this.http.post<UserDto>(this.base, req);
  }

  update(id: string, req: UpdateUserRequest): Observable<UserDto> {
    return this.http.put<UserDto>(`${this.base}/${id}`, req);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }
}
