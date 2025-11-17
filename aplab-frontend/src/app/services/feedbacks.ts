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

export interface MonthlyAverageDto {
  seasonId: number;
  monthIndex: number;
  monthStartUtc: string;
  monthEndUtc: string;
  averageScore: number | null;
  gradedFeedbacksCount: number;
}

export interface MentorInternAverageDto {
  internUserId: string;
  averageScore: number | null;
  gradedFeedbacksCount: number;
}

export interface MentorMonthlyAveragesDto {
  seasonId: number;
  monthIndex: number;
  monthStartUtc: string;
  monthEndUtc: string;
  items: MentorInternAverageDto[];
}

export interface MentorInternAverageRowDto {
  internUserId: string;
  fullName: string;
  email: string;
  averageScore: number | null;
  gradedFeedbacksCount: number;
}

export interface MentorMonthlyAveragesPageDto {
  seasonId: number;
  monthIndex: number;
  monthStartUtc: string;
  monthEndUtc: string;
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
  items: MentorInternAverageRowDto[];
}

@Injectable({ providedIn: 'root' })
export class Feedbacks {
  private http = inject(HttpClient);
  private base = '/api/feedbacks';

  getMine(page: number = 1): Observable<FeedbackDto[]> {
    return this.http.get<FeedbackDto[]>(`${this.base}/me`, { params: { page } });
  }

  getReceivedFromMentor(): Observable<FeedbackDto[]> {
    return this.http.get<FeedbackDto[]>(`${this.base}/me/received/mentor`);
  }

  getReceivedFromInterns(): Observable<FeedbackDto[]> {
    return this.http.get<FeedbackDto[]>(`${this.base}/me/received/interns`);
  }

  getSentByMe(): Observable<FeedbackDto[]> {
    return this.http.get<FeedbackDto[]>(`${this.base}/me/sent`);
  }

  getForAdmin(page: number = 1, seasonId?: number): Observable<FeedbackDto[]> {
    const params: any = { page };
    if (seasonId != null) params.seasonId = seasonId;
    return this.http.get<FeedbackDto[]>(this.base, { params });
  }

  getMineByMonth(monthIndex: number, page: number = 1): Observable<FeedbackDto[]> {
    return this.http.get<FeedbackDto[]>(`${this.base}/me/month/${monthIndex}`, { params: { page } });
  }

  getMyMonthlyAverage(monthIndex: number): Observable<MonthlyAverageDto> {
    return this.http.get<MonthlyAverageDto>(`${this.base}/me/month/${monthIndex}/average`);
  }

  getMentorByMonth(seasonId: number, monthIndex: number, page: number = 1): Observable<FeedbackDto[]> {
    return this.http.get<FeedbackDto[]>(`${this.base}/me/season/${seasonId}/month/${monthIndex}`, { params: { page } });
  }

  getMentorMonthlyAverages(seasonId: number, monthIndex: number): Observable<MentorMonthlyAveragesDto> {
    return this.http.get<MentorMonthlyAveragesDto>(`${this.base}/me/season/${seasonId}/month/${monthIndex}/averages`);
  }

  getMentorMonthlyAveragesPaged(
    seasonId: number,
    monthIndex: number,
    opts?: { sortBy?: 'name'|'grade'; sortDir?: 'asc'|'desc'; page?: number; pageSize?: number }
  ): Observable<MentorMonthlyAveragesPageDto> {
    const params: any = {
      seasonId,
      monthIndex,
      sortBy: opts?.sortBy ?? 'grade',
      sortDir: opts?.sortDir ?? 'desc',
      page: opts?.page ?? 1,
      pageSize: opts?.pageSize ?? 10
    };
    return this.http.get<MentorMonthlyAveragesPageDto>(`${this.base}/mentor/averages`, { params });
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
