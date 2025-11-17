import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { Navbar } from '../../../shared/navbar/navbar';
import { SeasonList } from '../../../shared/season-list/season-list';
import { FeedbackCard } from '../../../shared/feedback-card/feedback-card';
import { Seasons, SeasonDto, UserDto as SeasonUserDto } from '../../../services/seasons';
import { Auth } from '../../../services/auth';
import { Feedbacks, FeedbackDto } from '../../../services/feedbacks';
import { Role } from '../../../services/users';

type MeDto = {
  name?: string | null;
  email?: string | null;
  roleName?: Role | null;
};

type UserView = {
  fullName: string;
  roleName: Role | null;
};

@Component({
  selector: 'app-intern-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, Navbar, SeasonList, FeedbackCard],
  templateUrl: './intern-dashboard.html',
  styleUrl: './intern-dashboard.css'
})
export class InternDashboard implements OnInit {
  private router = inject(Router);
  private auth = inject(Auth);
  private http = inject(HttpClient);
  private seasonsApi = inject(Seasons);
  private feedbacksApi = inject(Feedbacks);

  readonly pageSize = 5;

  loading = true;
  feedbackLoading = true;

  mySeason: SeasonDto | null = null;
  others: SeasonDto[] = [];
  peers: SeasonUserDto[] = [];

  activeTab: 'seasons' | 'feedbacks' = 'seasons';

  allFeedbacks: FeedbackDto[] = [];
  givenFeedbacks: FeedbackDto[] = [];
  mentorFeedbacks: FeedbackDto[] = [];
  internFeedbacks: FeedbackDto[] = [];

  givenPage = 1;
  givenTotalPages = 0;
  givenPages: number[] = [];

  mentorPage = 1;
  mentorTotalPages = 0;
  mentorPages: number[] = [];

  internPage = 1;
  internTotalPages = 0;
  internPages: number[] = [];

  meId: string | null = null;
  meName = '';
  meRole: Role | null = 'intern';

  userMap: Record<string, UserView> = {};

  ngOnInit(): void {
    if (!this.auth.isLoggedIn()) {
      this.router.navigate(['/login']);
      return;
    }

    this.loadSeasons();
    this.loadFeedbacks();
  }

  get givenPageItems(): FeedbackDto[] {
    const start = (this.givenPage - 1) * this.pageSize;
    return this.givenFeedbacks.slice(start, start + this.pageSize);
  }

  get mentorPageItems(): FeedbackDto[] {
    const start = (this.mentorPage - 1) * this.pageSize;
    return this.mentorFeedbacks.slice(start, start + this.pageSize);
  }

  get internPageItems(): FeedbackDto[] {
    const start = (this.internPage - 1) * this.pageSize;
    return this.internFeedbacks.slice(start, start + this.pageSize);
  }

  private loadSeasons(): void {
    this.seasonsApi.getMySeason().subscribe({
      next: s => {
        this.mySeason = s ?? null;
        if (this.mySeason) {
          this.seasonsApi.getMySeasonUsers().subscribe({
            next: u => {
              this.peers = u;
              this.buildUserMapFromPeers();
              this.loadOthers();
            },
            error: () => {
              this.peers = [];
              this.loadOthers();
            }
          });
        } else {
          this.loadOthers();
        }
      },
      error: () => {
        this.mySeason = null;
        this.loadOthers();
      }
    });
  }

  private loadOthers(): void {
    this.seasonsApi.getAll().subscribe({
      next: ss => {
        const mineId = this.mySeason?.id ?? -1;
        this.others = (ss || []).filter(s => s.id !== mineId);
        this.loading = false;
      },
      error: () => {
        this.others = [];
        this.loading = false;
      }
    });
  }

  private loadFeedbacks(): void {
    this.feedbackLoading = true;

    this.http
      .get<MeDto>('/api/users/me')
      .pipe(
        catchError(() =>
          of({
            name: this.auth.user()?.name ?? null,
            email: this.auth.user()?.email ?? null,
            roleName: 'intern' as Role
          } as MeDto)
        )
      )
      .subscribe(me => {
        this.meName = me.name ?? '';
        this.meRole = (me.roleName as Role | null) ?? 'intern';

        this.feedbacksApi.getMine().subscribe({
          next: list => {
            this.allFeedbacks = list || [];
            this.detectMeIdFromFeedbacks();
            this.buildUserMapFromPeers();
            this.splitFeedbacks();
            this.feedbackLoading = false;
          },
          error: () => {
            this.allFeedbacks = [];
            this.givenFeedbacks = [];
            this.mentorFeedbacks = [];
            this.internFeedbacks = [];
            this.updatePagination();
            this.feedbackLoading = false;
          }
        });
      });
  }

  private detectMeIdFromFeedbacks(): void {
    if (this.meId || !this.allFeedbacks.length) return;

    let candidates = new Set<string>();
    const first = this.allFeedbacks[0];
    candidates.add(first.senderUserId);
    candidates.add(first.receiverUserId);

    for (let i = 1; i < this.allFeedbacks.length && candidates.size > 1; i++) {
      const f = this.allFeedbacks[i];
      const idsThis = new Set<string>([f.senderUserId, f.receiverUserId]);
      for (const c of Array.from(candidates)) {
        if (!idsThis.has(c)) {
          candidates.delete(c);
        }
      }
    }

    this.meId = candidates.size ? Array.from(candidates)[0] : null;

    if (this.meId && this.meName) {
      this.userMap[this.meId] = {
        fullName: this.meName,
        roleName: this.meRole
      };
    }
  }

  private buildUserMapFromPeers(): void {
    for (const p of this.peers || []) {
      this.userMap[p.id] = {
        fullName: p.fullName,
        roleName: (p.roleName as Role | null) ?? null
      };
    }

    if (this.meId && this.meName && !this.userMap[this.meId]) {
      this.userMap[this.meId] = {
        fullName: this.meName,
        roleName: this.meRole
      };
    }
  }

  private splitFeedbacks(): void {
    const list = this.allFeedbacks || [];

    if (!this.meId) {
      this.givenFeedbacks = [];
      this.mentorFeedbacks = [];
      this.internFeedbacks = [];
      this.updatePagination();
      return;
    }

    this.givenFeedbacks = list.filter(f => f.senderUserId === this.meId);
    const received = list.filter(f => f.receiverUserId === this.meId);
    this.mentorFeedbacks = received.filter(f => !!f.grade);
    this.internFeedbacks = received.filter(f => !f.grade);

    this.updatePagination();
  }

  private updatePagination(): void {
    this.givenTotalPages = Math.ceil(this.givenFeedbacks.length / this.pageSize);
    this.givenPages = Array.from({ length: this.givenTotalPages }, (_, i) => i + 1);
    if (this.givenPage > this.givenTotalPages && this.givenTotalPages > 0) {
      this.givenPage = this.givenTotalPages;
    } else if (this.givenTotalPages === 0) {
      this.givenPage = 1;
    }

    this.mentorTotalPages = Math.ceil(this.mentorFeedbacks.length / this.pageSize);
    this.mentorPages = Array.from({ length: this.mentorTotalPages }, (_, i) => i + 1);
    if (this.mentorPage > this.mentorTotalPages && this.mentorTotalPages > 0) {
      this.mentorPage = this.mentorTotalPages;
    } else if (this.mentorTotalPages === 0) {
      this.mentorPage = 1;
    }

    this.internTotalPages = Math.ceil(this.internFeedbacks.length / this.pageSize);
    this.internPages = Array.from({ length: this.internTotalPages }, (_, i) => i + 1);
    if (this.internPage > this.internTotalPages && this.internTotalPages > 0) {
      this.internPage = this.internTotalPages;
    } else if (this.internTotalPages === 0) {
      this.internPage = 1;
    }
  }

  givenPageClick(p: number): void {
    if (p !== this.givenPage) this.givenPage = p;
  }

  mentorPageClick(p: number): void {
    if (p !== this.mentorPage) this.mentorPage = p;
  }

  internPageClick(p: number): void {
    if (p !== this.internPage) this.internPage = p;
  }

  openSeason(id: number) {
    this.router.navigate(['/seasons', id]);
  }
}
