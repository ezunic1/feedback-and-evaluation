import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

export type MeResponse = {
  name?: string;
  email?: string;
  role?: 'guest' | 'intern' | 'mentor' | 'admin' | string;
  internSeasonName?: string | null;
  mentorSeasonName?: string | null;
};

@Injectable({ providedIn: 'root' })
export class ProfileService {
  constructor(private http: HttpClient) {}
  async getMe(): Promise<MeResponse | null> {
    try {
      return await firstValueFrom(this.http.get<MeResponse>('/api/users/me'));
    } catch {
      return null;
    }
  }
}
