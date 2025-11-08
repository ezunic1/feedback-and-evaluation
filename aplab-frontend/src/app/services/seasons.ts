import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpResponse } from '@angular/common/http';
import { map, Observable } from 'rxjs';

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

@Injectable({ providedIn: 'root' })
export class Seasons {
  private http = inject(HttpClient);
  private base = '/api/seasons';

  getAll(): Observable<SeasonDto[]> {
    return this.http.get<SeasonDto[]>(this.base);
  }

  getById(id: number): Observable<SeasonDto> {
    return this.http.get<SeasonDto>(`${this.base}/${id}`);
  }

  getUsers(id: number): Observable<UserDto[]> {
    return this.http.get<UserDto[]>(`${this.base}/${id}/users`);
  }

  getMySeason(): Observable<SeasonDto | null> {
    return this.http.get<SeasonDto>(`${this.base}/me`, { observe: 'response' })
      .pipe(map((r: HttpResponse<SeasonDto>) => r.status === 204 ? null : (r.body as SeasonDto)));
  }

  getMySeasonUsers(): Observable<UserDto[]> {
    return this.http.get<UserDto[]>(`${this.base}/me/users`);
  }

  create(req: CreateSeasonRequest): Observable<SeasonDto> {
    return this.http.post<SeasonDto>(this.base, req);
  }

  update(id: number, req: UpdateSeasonRequest): Observable<SeasonDto> {
    return this.http.put<SeasonDto>(`${this.base}/${id}`, req);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  assignMentor(id: number, mentorId: string | null): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/assign-mentor`, { mentorId });
  }

  addUser(id: number, userId: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/users/${userId}`, {});
  }

  removeUser(id: number, userId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}/users/${userId}`);
  }

  mentorAddUser(seasonId: number, userId: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${seasonId}/users/${userId}/by-mentor`, {});
  }

  mentorRemoveUser(seasonId: number, userId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${seasonId}/users/${userId}/by-mentor`);
  }
}
