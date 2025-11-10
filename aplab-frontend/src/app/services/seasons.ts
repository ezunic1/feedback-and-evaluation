import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpResponse } from '@angular/common/http';
import { Observable, map, shareReplay, tap } from 'rxjs';

export interface SeasonDto {
  id: number;
  name: string;
  startDate: string;
  endDate: string;
  mentorId: string | null;
  mentorName: string | null;
  usersCount: number;
}

export interface UserDto {
  id: string;
  fullName: string;
  email: string;
  roleName: string | null;
  keycloakId: string;
  seasonId: number | null;
}

export interface CreateSeasonRequest {
  name: string;
  startDate: string;
  endDate: string;
  mentorId: string | null;
}

export interface UpdateSeasonRequest {
  name?: string;
  startDate?: string;
  endDate?: string;
  mentorId?: string | null;
}

export interface SeasonOption {
  id: number;
  name: string;
}

@Injectable({ providedIn: 'root' })
export class Seasons {
  private http = inject(HttpClient);
  private base = '/api/seasons';
  private cacheAll$?: Observable<SeasonDto[]>;
  private cacheOptions$?: Observable<SeasonOption[]>;

  private invalidateCaches(): void {
    this.cacheAll$ = undefined;
    this.cacheOptions$ = undefined;
  }

  getAll(): Observable<SeasonDto[]> {
    return this.http.get<SeasonDto[]>(this.base);
  }

  getAllCached(): Observable<SeasonDto[]> {
    if (!this.cacheAll$) {
      this.cacheAll$ = this.http.get<SeasonDto[]>(this.base).pipe(shareReplay(1));
    }
    return this.cacheAll$;
  }

  getBrowse(): Observable<SeasonDto[]> {
    return this.http.get<SeasonDto[]>(`${this.base}/browse`);
  }

  getOptions(): Observable<SeasonOption[]> {
    return this.getAll().pipe(map(list => list.map(s => ({ id: s.id, name: s.name }))));
  }

  getOptionsCached(): Observable<SeasonOption[]> {
    if (!this.cacheOptions$) {
      this.cacheOptions$ = this.getAllCached().pipe(
        map(list => list.map(s => ({ id: s.id, name: s.name }))),
        shareReplay(1)
      );
    }
    return this.cacheOptions$;
  }

  getById(id: number): Observable<SeasonDto> {
    return this.http.get<SeasonDto>(`${this.base}/${id}`);
  }

  getUsers(id: number): Observable<UserDto[]> {
    return this.http.get<UserDto[]>(`${this.base}/${id}/users`);
  }

  getMySeason(): Observable<SeasonDto | null> {
    return this.http
      .get<SeasonDto>(`${this.base}/me`, { observe: 'response' })
      .pipe(map((r: HttpResponse<SeasonDto>) => (r.status === 204 ? null : (r.body as SeasonDto))));
  }

  getMySeasonUsers(): Observable<UserDto[]> {
    return this.http.get<UserDto[]>(`${this.base}/me/users`);
  }

  create(req: CreateSeasonRequest): Observable<SeasonDto> {
    return this.http.post<SeasonDto>(this.base, req).pipe(tap(() => this.invalidateCaches()));
  }

  update(id: number, req: UpdateSeasonRequest): Observable<SeasonDto> {
    return this.http.put<SeasonDto>(`${this.base}/${id}`, req).pipe(tap(() => this.invalidateCaches()));
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`).pipe(tap(() => this.invalidateCaches()));
  }

  assignMentor(id: number, mentorId: string | null): Observable<void> {
    return this.http
      .post<void>(`${this.base}/${id}/assign-mentor`, { mentorId })
      .pipe(tap(() => this.invalidateCaches()));
  }

  addUser(id: number, userId: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/users/${userId}`, {}).pipe(tap(() => this.invalidateCaches()));
  }

  removeUser(id: number, userId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}/users/${userId}`).pipe(tap(() => this.invalidateCaches()));
  }

  mentorAddUser(seasonId: number, userId: string): Observable<void> {
    return this.http
      .post<void>(`${this.base}/${seasonId}/users/${userId}/by-mentor`, {})
      .pipe(tap(() => this.invalidateCaches()));
  }

  mentorRemoveUser(seasonId: number, userId: string): Observable<void> {
    return this.http
      .delete<void>(`${this.base}/${seasonId}/users/${userId}/by-mentor`)
      .pipe(tap(() => this.invalidateCaches()));
  }
}
