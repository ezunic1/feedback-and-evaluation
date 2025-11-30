import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';

export type CreateDeleteRequest = { feedbackId: number; reason: string };
export type DeleteRequestItem = { id: number; feedbackId: number; senderUserId: string; reason: string; createdAtUtc: string };

@Injectable({ providedIn: 'root' })
export class DeleteRequests {
  private http = inject(HttpClient);
  private base = '/api/delete-requests';

  create(body: CreateDeleteRequest) { return this.http.post<number>(`${this.base}`, body); }
  getAll() { return this.http.get<DeleteRequestItem[]>(`${this.base}`); }
  approve(id: number) { return this.http.post<void>(`${this.base}/${id}/approve`, {}); }
  reject(id: number) { return this.http.post<void>(`${this.base}/${id}/reject`, {}); }
}
