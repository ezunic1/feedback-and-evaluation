import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

export type MeResponse = {
  name?: string;
  email?: string;
  role?: 'guest' | 'intern' | 'mentor' | 'admin' | string;
  internSeasonName?: string | null;
  mentorSeasonName?: string | null;
  description?: string | null;
};

export type UpdateMeRequest = {
  fullName: string;
  description?: string | null;
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

  async updateMe(body: UpdateMeRequest): Promise<MeResponse> {
    return await firstValueFrom(this.http.put<MeResponse>('/api/users/me', body));
  }

  async getPasswordChangeUrl(redirectUri?: string): Promise<string> {
    const params = redirectUri ? new HttpParams().set('redirectUri', redirectUri) : undefined;
    const res = await firstValueFrom(this.http.get<{ url?: string; Url?: string }>('/api/auth/first-login-url', { params }));
    return res.url ?? res.Url ?? '';
  }
}
