import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { Navbar } from '../../../shared/navbar/navbar';
import { SeasonList } from '../../../shared/season-list/season-list';
import { UsersTable } from '../../../shared/users-table/users-table';
import { FeedbackList } from '../../../shared/feedback-list/feedback-list';
import { FeedbackCard } from '../../../shared/feedback-card/feedback-card';
import { ConfirmDelete } from '../../../shared/confirm-delete/confirm-delete';

import { Seasons, SeasonDto } from '../../../services/seasons';
import { Users, UserDto } from '../../../services/users';
import { Auth } from '../../../services/auth';
import { FeedbackDto, Feedbacks } from '../../../services/feedbacks';
import { DeleteRequests } from '../../../services/delete-requests';

type Tab = 'seasons' | 'users' | 'feedbacks' | 'requests';

type DeleteRequestItem = {
  id: number;
  feedbackId: number;
  senderUserId: string;
  reason: string;
  createdAtUtc: string;
  feedback?: FeedbackDto | null;
  fbLoaded?: boolean;
};

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, Navbar, SeasonList, UsersTable, FeedbackList, FeedbackCard, ConfirmDelete],
  templateUrl: './admin-dashboard.html',
  styleUrls: ['./admin-dashboard.css']
})
export class AdminDashboard implements OnInit {
  private router = inject(Router);
  private auth = inject(Auth);
  private seasonsApi = inject(Seasons);
  private usersApi = inject(Users);
  private feedbacksApi = inject(Feedbacks);
  private delReqApi = inject(DeleteRequests);

  loadingSeasons = true;
  seasons: SeasonDto[] = [];
  filteredSeasons: SeasonDto[] = [];
  qSeason = '';

  activeTab: Tab = 'seasons';

  loadingRequests = false;
  requests: DeleteRequestItem[] = [];

  private expandedReq = new Set<number>();
  private usersById = new Map<string, string>();
  private reqLoading = new Map<number, 'approve' | 'reject' | null>();

  confirmOpen = false;
  confirmMode: 'approve' | 'reject' | null = null;
  confirmTarget: DeleteRequestItem | null = null;
  confirmTitle = '';
  confirmMessage = '';
  confirmLabel = '';
  confirmCancelLabel = 'Cancel';
  confirmLoading = false;

  get isAdmin(): boolean {
    return this.auth.hasRole('admin');
  }

  loadingOf(id: number): 'approve' | 'reject' | null | undefined {
    return this.reqLoading.get(id);
  }

  ngOnInit(): void {
    if (!this.auth.isLoggedIn()) {
      this.router.navigate(['/login']);
      return;
    }

    this.seasonsApi.getAll().subscribe({
      next: ss => {
        this.seasons = ss;
        this.loadingSeasons = false;
        this.applySeasonFilters();
      },
      error: () => {
        this.seasons = [];
        this.loadingSeasons = false;
        this.applySeasonFilters();
      }
    });

    this.loadRequests();
  }

  addSeason() {
    this.router.navigate(['/seasons/new']);
  }

  addUser() {
    this.router.navigate(['/users/new']);
  }

  openSeason(id: number) {
    this.router.navigate(['/seasons', id]);
  }

  openUser(id: string) {
    this.router.navigate(['/users', id]);
  }

  applySeasonFilters() {
    const q = this.qSeason.trim().toLowerCase();
    this.filteredSeasons = this.seasons.filter(s => {
      const name = (s.name || '').toLowerCase();
      return q ? name.includes(q) : true;
    });
  }

  toggleRequest(id: number) {
    if (this.expandedReq.has(id)) this.expandedReq.delete(id);
    else this.expandedReq.add(id);
  }

  isReqExpanded(id: number) {
    return this.expandedReq.has(id);
  }

  nameOf(id?: string | null): string {
    if (!id) return '';
    const cached = this.usersById.get(id);
    if (cached) return cached;

    this.usersApi.getById(id).subscribe({
      next: (u: UserDto) => {
        const name = (u.fullName || '').trim() || u.email || id;
        this.usersById.set(id, name);
      },
      error: () => this.usersById.set(id, id)
    });

    return '';
  }

  private setLoading(id: number, state: 'approve' | 'reject' | null) {
    this.reqLoading.set(id, state);
  }

  private loadRequests() {
    this.loadingRequests = true;

    this.delReqApi.getAll().subscribe({
      next: list => {
        this.requests = (list || [])
          .map(r => ({ ...r, fbLoaded: false }))
          .sort((a, b) => new Date(b.createdAtUtc).getTime() - new Date(a.createdAtUtc).getTime());

        const needNames = new Set<string>();
        for (const r of this.requests) if (r.senderUserId) needNames.add(r.senderUserId);
        for (const id of needNames) this.nameOf(id);

        this.feedbacksApi.getForAdmin(1).pipe(
          catchError(() => of([] as FeedbackDto[]))
        ).subscribe(all => {
          const byId = new Map<number, FeedbackDto>();
          for (const f of all || []) byId.set(f.id, f);

          this.requests = this.requests.map(r => ({
            ...r,
            feedback: byId.get(r.feedbackId) ?? null,
            fbLoaded: true
          }));

          const moreNames = new Set<string>();
          for (const r of this.requests) {
            if (r.feedback?.senderUserId) moreNames.add(r.feedback.senderUserId);
            if (r.feedback?.receiverUserId) moreNames.add(r.feedback.receiverUserId);
          }
          for (const id of moreNames) this.nameOf(id);

          this.loadingRequests = false;
        });
      },
      error: () => {
        this.requests = [];
        this.loadingRequests = false;
      }
    });
  }

  openConfirm(mode: 'approve' | 'reject', r: DeleteRequestItem, ev?: Event) {
    ev?.stopPropagation();
    if (!this.isAdmin) return;
    this.confirmMode = mode;
    this.confirmTarget = r;
    this.confirmTitle = mode === 'approve' ? 'Approve delete request' : 'Reject delete request';
    this.confirmMessage = mode === 'approve'
      ? 'Are you sure you want to approve deletion of this feedback?'
      : 'Are you sure you want to reject this delete request?';
    this.confirmLabel = mode === 'approve' ? 'Approve' : 'Reject';
    this.confirmCancelLabel = 'Cancel';
    this.confirmLoading = false;
    this.confirmOpen = true;
  }

  onCancelModal() {
    if (this.confirmLoading) return;
    this.confirmOpen = false;
    this.confirmMode = null;
    this.confirmTarget = null;
  }

  onConfirmModal() {
    if (!this.confirmMode || !this.confirmTarget) {
      this.confirmOpen = false;
      return;
    }
    this.confirmLoading = true;
    if (this.confirmMode === 'approve') this.approveRequest(this.confirmTarget);
    else this.rejectRequest(this.confirmTarget);
  }

  private clearConfirmState() {
    this.confirmOpen = false;
    this.confirmMode = null;
    this.confirmTarget = null;
    this.confirmLoading = false;
  }

  approveRequest(r: DeleteRequestItem) {
    if (!this.isAdmin) return;
    this.setLoading(r.id, 'approve');
    this.delReqApi.approve(r.id).subscribe({
      next: () => {
        this.requests = this.requests.filter(x => x.id !== r.id);
        this.reqLoading.delete(r.id);
        this.clearConfirmState();
      },
      error: () => {
        this.setLoading(r.id, null);
        this.confirmLoading = false;
        alert('Failed to approve delete request.');
      }
    });
  }

  rejectRequest(r: DeleteRequestItem) {
    if (!this.isAdmin) return;
    this.setLoading(r.id, 'reject');
    this.delReqApi.reject(r.id).subscribe({
      next: () => {
        this.requests = this.requests.filter(x => x.id !== r.id);
        this.reqLoading.delete(r.id);
        this.clearConfirmState();
      },
      error: () => {
        this.setLoading(r.id, null);
        this.confirmLoading = false;
        alert('Failed to reject delete request.');
      }
    });
  }
}
