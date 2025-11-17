// src/app/services/feedbacks.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface GradeDto {
  careerSkills: number;
  communication: number;
  collaboration: number;
}

export interface FeedbackDto {
  id: number;
  seasonId: number;
  senderUserId: string;
  receiverUserId: string;
  comment: string;
  createdAtUtc: string;
  grade: GradeDto | null;
}

export interface CreateInternFeedbackRequest {
  receiverUserId: string;
  comment: string;
}

export interface CreateMentorFeedbackRequest {
  receiverUserId: string;
  comment: string;
  careerSkills: number;
  communication: number;
  collaboration: number;
}

@Injectable({ providedIn: 'root' })
export class Feedbacks {
  private http = inject(HttpClient);
  private base = '/api/feedbacks';

  getMine(page: number = 1): Observable<FeedbackDto[]> {
    return this.http.get<FeedbackDto[]>(`${this.base}/me`, { params: { page } });
  }

  getForAdmin(page: number = 1, seasonId?: number): Observable<FeedbackDto[]> {
    const params: any = { page };
    if (seasonId != null) params.seasonId = seasonId;
    return this.http.get<FeedbackDto[]>(this.base, { params });
  }

  createAsIntern(req: CreateInternFeedbackRequest): Observable<FeedbackDto> {
    return this.http.post<FeedbackDto>(`${this.base}/intern`, req);
  }

  createAsMentor(req: CreateMentorFeedbackRequest): Observable<FeedbackDto> {
    return this.http.post<FeedbackDto>(`${this.base}/mentor`, req);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }
}
