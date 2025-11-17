import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Feedbacks, FeedbackDto } from '../../services/feedbacks';
import { Seasons, SeasonDto } from '../../services/seasons';
import { Users, UserListItem, Role } from '../../services/users';
import { Auth } from '../../services/auth';
import { InternGrades } from '../intern-grades/intern-grades';
import { FeedbackCard } from '../feedback-card/feedback-card';

type DashRole = 'admin' | 'mentor' | 'other';
type SeasonVM = { id: number; name: string; startDateUtc?: string | null; endDateUtc?: string | null; mentorUserId?: string | null; mentorName?: string | null; };

@Component({
  selector: 'app-feedback-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, InternGrades, FeedbackCard],
  templateUrl: './feedback-list.html',
  styleUrls: ['./feedback-list.css']
})
export class FeedbackList implements OnInit {
  private feedbackApi = inject(Feedbacks);
  private seasonsApi = inject(Seasons);
  private usersApi = inject(Users);
  private auth = inject(Auth);

  role: DashRole = 'other';
  seasonsVm: SeasonVM[] = [];
  selectedSeasonId: number | null = null;

  loadingSeasons = true;
  loadingFeedbacks = true;
  loadingUsers = true;

  allFeedbacks: FeedbackDto[] = [];
  internToIntern: FeedbackDto[] = [];
  internToMentor: FeedbackDto[] = [];

  readonly pageSize = 5;
  i2iPage = 1;
  i2iTotalPages = 0;
  i2iPages: number[] = [];
  i2mPage = 1;
  i2mTotalPages = 0;
  i2mPages: number[] = [];

  private usersById = new Map<string, { name: string; role: Role | null }>();

  ngOnInit(): void {
    this.resolveRole();
    this.loadSeasons().then(() => {
      this.pickDefaultSeason();
      this.loadUsersForSelectedSeason();
      this.refreshFeedbacks();
    });
  }

  onSeasonChange(): void {
    this.i2iPage = 1;
    this.i2mPage = 1;
    this.loadUsersForSelectedSeason();
    this.refreshFeedbacks();
  }

  get i2iPageItems(): FeedbackDto[] {
    const start = (this.i2iPage - 1) * this.pageSize;
    return this.internToIntern.slice(start, start + this.pageSize);
  }

  get i2mPageItems(): FeedbackDto[] {
    const start = (this.i2mPage - 1) * this.pageSize;
    return this.internToMentor.slice(start, start + this.pageSize);
  }

  i2iPageClick(p: number): void {
    if (p !== this.i2iPage) this.i2iPage = p;
  }

  i2mPageClick(p: number): void {
    if (p !== this.i2mPage) this.i2mPage = p;
  }

  nameOf(id: string | null | undefined): string {
    if (!id) return '';
    const hit = this.usersById.get(id);
    if (hit) return hit.name;
    const s = this.currentSeasonVm();
    if (s && s.mentorUserId === id) return s.mentorName || 'Mentor';
    return '';
  }

  roleOf(id: string | null | undefined): Role | string | null {
    if (!id) return null;
    const hit = this.usersById.get(id);
    if (hit) return hit.role ?? null;
    const s = this.currentSeasonVm();
    if (s && s.mentorUserId === id) return 'mentor';
    return null;
  }

  private resolveRole(): void {
    if (this.auth.hasRole('admin')) { this.role = 'admin'; return; }
    if (this.auth.hasRole('mentor')) { this.role = 'mentor'; return; }
    this.role = 'other';
  }

  private toVm(s: SeasonDto | any): SeasonVM {
    return {
      id: Number(s?.id),
      name: String(s?.name ?? 'Season'),
      startDateUtc: s?.startDateUtc ?? s?.startDate ?? null,
      endDateUtc: s?.endDateUtc ?? s?.endDate ?? null,
      mentorUserId: s?.mentor?.id ?? s?.mentorId ?? s?.mentorUserId ?? null,
      mentorName: s?.mentor?.fullName ?? s?.mentorName ?? null
    };
  }

  private async loadSeasons(): Promise<void> {
    return new Promise<void>((resolve) => {
      this.seasonsApi.getAll().subscribe({
        next: ss => {
          const allVm = (ss as any[]).map(x => this.toVm(x));

          if (this.role === 'mentor') {
            const user = this.auth.user();
            const uid = (this.auth.userId() ?? '').toLowerCase().trim();
            const myName = (user?.name ?? '').toLowerCase().trim();
            this.seasonsVm = allVm.filter(s => {
              const mid = (s.mentorUserId ?? '').toLowerCase().trim();
              const mname = (s.mentorName ?? '').toLowerCase().trim();

              const byId = uid && mid === uid;
              const byName = myName && mname === myName;

              return byId || byName;
            });
          } else {
            this.seasonsVm = allVm;
          }

          this.loadingSeasons = false;
          resolve();
        },
        error: () => {
          this.seasonsVm = [];
          this.loadingSeasons = false;
          resolve();
        }
      });
    });
  }

  private currentSeasonVm(): SeasonVM | undefined {
    if (this.selectedSeasonId == null) return undefined;
    return this.seasonsVm.find(s => s.id === this.selectedSeasonId);
  }

  private pickDefaultSeason(): void {
    if (!this.seasonsVm.length) { this.selectedSeasonId = null; return; }
    const now = Date.now();
    const parse = (d?: string | null) => d ? Date.parse(d) : NaN;
    const active = this.seasonsVm
      .filter(s => {
        const a = parse(s.startDateUtc);
        const b = parse(s.endDateUtc);
        return !isNaN(a) && !isNaN(b) && a <= now && now <= b;
      })
      .sort((x, y) => parse(y.startDateUtc) - parse(x.startDateUtc));
    if (active.length) { this.selectedSeasonId = active[0].id; return; }
    const finished = this.seasonsVm
      .filter(s => {
        const b = parse(s.endDateUtc);
        return !isNaN(b) && b < now;
      })
      .sort((x, y) => parse(y.endDateUtc) - parse(x.endDateUtc));
    if (finished.length) { this.selectedSeasonId = finished[0].id; return; }
    this.selectedSeasonId = this.seasonsVm[0].id ?? null;
  }

  private loadUsersForSelectedSeason(): void {
    if (!this.selectedSeasonId) { this.usersById.clear(); this.loadingUsers = false; return; }
    this.loadingUsers = true;
    this.usersApi.getBySeason(this.selectedSeasonId).subscribe({
      next: us => {
        this.usersById.clear();
        for (const u of us as UserListItem[]) {
          const name = (u.fullName ?? '').trim() || u.email;
          this.usersById.set(u.id, { name, role: u.role });
        }
        this.ensureMentorInMap();
        this.loadingUsers = false;
      },
      error: () => {
        this.usersById.clear();
        this.ensureMentorInMap();
        this.loadingUsers = false;
      }
    });
  }

  private ensureMentorInMap(): void {
    const s = this.currentSeasonVm();
    const mid = s?.mentorUserId || null;
    if (!mid) return;
    if (this.usersById.has(mid)) return;
    if (s?.mentorName) {
      this.usersById.set(mid, { name: s.mentorName, role: 'mentor' });
      return;
    }
    this.usersApi.getById(mid).subscribe({
      next: u => {
        const name = (u.fullName ?? '').trim() || u.email || 'Mentor';
        this.usersById.set(u.id, { name, role: 'mentor' });
      },
      error: () => {
        this.usersById.set(mid, { name: 'Mentor', role: 'mentor' as Role });
      }
    });
  }

  private refreshFeedbacks(): void {
    if (!this.selectedSeasonId) {
      this.allFeedbacks = [];
      this.splitLists();
      return;
    }

    this.loadingFeedbacks = true;

    if (this.role === 'admin') {
      this.feedbackApi.getForAdmin(1, this.selectedSeasonId).subscribe({
        next: list => {
          this.allFeedbacks = list || [];
          this.splitLists();
          this.loadingFeedbacks = false;
        },
        error: () => {
          this.allFeedbacks = [];
          this.splitLists();
          this.loadingFeedbacks = false;
        }
      });
      return;
    }

    if (this.role === 'mentor') {
      this.feedbackApi.getMine(1).subscribe({
        next: list => {
          const seasonId = this.selectedSeasonId!;
          this.allFeedbacks = (list || []).filter(f => f.seasonId === seasonId);
          this.splitLists();
          this.loadingFeedbacks = false;
        },
        error: () => {
          this.allFeedbacks = [];
          this.splitLists();
          this.loadingFeedbacks = false;
        }
      });
      return;
    }

    this.allFeedbacks = [];
    this.splitLists();
    this.loadingFeedbacks = false;
  }

  private getSelectedSeasonMentorId(): string | null {
    const s = this.currentSeasonVm();
    if (!s) return null;
    return s.mentorUserId ?? null;
  }

  private splitLists(): void {
    const mentorId = this.getSelectedSeasonMentorId();
    const seasonId = this.selectedSeasonId;
    const inSeason = this.allFeedbacks.filter(f => f.seasonId === seasonId);
    const isInternToMentor = (f: FeedbackDto) => f.grade == null && mentorId && f.receiverUserId === mentorId;
    const isInternToIntern = (f: FeedbackDto) => f.grade == null && (!mentorId || f.receiverUserId !== mentorId);
    this.internToMentor = inSeason.filter(isInternToMentor);
    this.internToIntern = inSeason.filter(isInternToIntern);
    this.updatePagination();
  }

  private updatePagination(): void {
    this.i2iTotalPages = Math.ceil(this.internToIntern.length / this.pageSize);
    this.i2iPages = Array.from({ length: this.i2iTotalPages || 1 }, (_, i) => i + 1);
    if (this.i2iTotalPages === 0) this.i2iPage = 1;
    else if (this.i2iPage > this.i2iTotalPages) this.i2iPage = this.i2iTotalPages;

    this.i2mTotalPages = Math.ceil(this.internToMentor.length / this.pageSize);
    this.i2mPages = Array.from({ length: this.i2mTotalPages || 1 }, (_, i) => i + 1);
    if (this.i2mTotalPages === 0) this.i2mPage = 1;
    else if (this.i2mPage > this.i2mTotalPages) this.i2mPage = this.i2mTotalPages;
  }
}
