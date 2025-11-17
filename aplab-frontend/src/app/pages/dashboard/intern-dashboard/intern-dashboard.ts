import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { of, forkJoin } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { Navbar } from '../../../shared/navbar/navbar';
import { SeasonList } from '../../../shared/season-list/season-list';
import { FeedbackCard } from '../../../shared/feedback-card/feedback-card';
import { Seasons, SeasonDto, UserDto as SeasonUserDto } from '../../../services/seasons';
import { Auth } from '../../../services/auth';
import { Feedbacks, FeedbackDto, MonthlyAverageDto } from '../../../services/feedbacks';
import { Role } from '../../../services/users';

type MeDto = {
  id?: string | null;
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

  meName = '';
  meRole: Role | null = 'intern';

  userMap: Record<string, UserView> = {};

  overviewLoading = false;
  monthlyOverview: MonthlyAverageDto[] = [];

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
          this.addMentorToUserMap(this.mySeason);
          this.seasonsApi.getMySeasonUsers().subscribe({
            next: u => {
              this.peers = u;
              this.buildUserMapFromPeers();
              this.loadOthers();
              this.tryComputeOverview();
            },
            error: () => {
              this.peers = [];
              this.loadOthers();
              this.tryComputeOverview();
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
            id: null,
            name: this.auth.user()?.name ?? null,
            email: this.auth.user()?.email ?? null,
            roleName: 'intern' as Role
          } as MeDto)
        )
      )
      .subscribe(me => {
        this.meName = me.name ?? '';
        this.meRole = (me.roleName as Role | null) ?? 'intern';

        forkJoin({
          recvMentor: this.feedbacksApi.getReceivedFromMentor(),
          recvInterns: this.feedbacksApi.getReceivedFromInterns(),
          sent: this.feedbacksApi.getSentByMe()
        }).subscribe({
          next: r => {
            this.mentorFeedbacks = r.recvMentor || [];
            this.internFeedbacks = r.recvInterns || [];
            this.givenFeedbacks = r.sent || [];
            this.addMentorToUserMap(this.mySeason);
            this.updatePagination();
            this.feedbackLoading = false;
            this.tryComputeOverview();
          },
          error: () => {
            this.mentorFeedbacks = [];
            this.internFeedbacks = [];
            this.givenFeedbacks = [];
            this.updatePagination();
            this.feedbackLoading = false;
            this.tryComputeOverview();
          }
        });
      });
  }

  private buildUserMapFromPeers(): void {
    for (const p of (this.peers || [])) {
      this.userMap[p.id] = {
        fullName: p.fullName,
        roleName: (p.roleName as Role | null) ?? null
      };
    }
    this.addMentorToUserMap(this.mySeason);
  }

  private addMentorToUserMap(season: SeasonDto | null): void {
    if (!season?.mentorId) return;
    this.userMap[season.mentorId] = {
      fullName: season.mentorName ?? 'Mentor',
      roleName: 'mentor'
    };
  }

  private updatePagination(): void {
    this.givenTotalPages = Math.ceil(this.givenFeedbacks.length / this.pageSize);
    this.givenPages = Array.from({ length: this.givenTotalPages || 1 }, (_, i) => i + 1);
    if (this.givenTotalPages === 0) this.givenPage = 1;
    else if (this.givenPage > this.givenTotalPages) this.givenPage = this.givenTotalPages;

    this.mentorTotalPages = Math.ceil(this.mentorFeedbacks.length / this.pageSize);
    this.mentorPages = Array.from({ length: this.mentorTotalPages || 1 }, (_, i) => i + 1);
    if (this.mentorTotalPages === 0) this.mentorPage = 1;
    else if (this.mentorPage > this.mentorTotalPages) this.mentorPage = this.mentorTotalPages;

    this.internTotalPages = Math.ceil(this.internFeedbacks.length / this.pageSize);
    this.internPages = Array.from({ length: this.internTotalPages || 1 }, (_, i) => i + 1);
    if (this.internTotalPages === 0) this.internPage = 1;
    else if (this.internPage > this.internTotalPages) this.internPage = this.internTotalPages;
  }

  private tryComputeOverview(): void {
    if (!this.mySeason) {
      this.monthlyOverview = [];
      return;
    }
    this.monthlyOverview = this.computeOverviewFromMentorFeedbacks(
      this.mySeason,
      this.mentorFeedbacks
    );
  }

  private computeOverviewFromMentorFeedbacks(season: SeasonDto, feedbacks: FeedbackDto[]): MonthlyAverageDto[] {
    const start = new Date(season.startDate as unknown as string);
    const rawEnd = new Date(season.endDate as unknown as string);
    const now = new Date();
    const end = now < rawEnd ? now : rawEnd;

    if (!(start instanceof Date) || !(end instanceof Date) || isNaN(start.getTime()) || isNaN(end.getTime()) || end <= start) {
      return [];
    }

    const starts: Date[] = [];
    const ends: Date[] = [];
    let s = new Date(Date.UTC(start.getUTCFullYear(), start.getUTCMonth(), start.getUTCDate()));
    while (s < end) {
      const n = this.addMonthsUtc(s, 1);
      const e = n < end ? n : end;
      starts.push(s);
      ends.push(e);
      s = n;
    }
    const nMonths = starts.length;
    if (nMonths === 0) return [];

    const sum: number[] = Array(nMonths).fill(0);
    const cnt: number[] = Array(nMonths).fill(0);

    for (const f of feedbacks || []) {
      if (!f.grade) continue;
      const t = new Date(f.createdAtUtc);
      if (t < starts[0] || t >= ends[nMonths - 1]) continue;
      for (let i = 0; i < nMonths; i++) {
        if (t >= starts[i] && t < ends[i]) {
          const g = f.grade;
          const avg = (g.careerSkills + g.communication + g.collaboration) / 3;
          sum[i] += avg;
          cnt[i] += 1;
          break;
        }
      }
    }

    const result: MonthlyAverageDto[] = [];
    for (let i = 0; i < nMonths; i++) {
      const avg = cnt[i] > 0 ? sum[i] / cnt[i] : null;
      result.push({
        seasonId: season.id,
        monthIndex: i + 1,
        monthStartUtc: starts[i].toISOString(),
        monthEndUtc: ends[i].toISOString(),
        averageScore: avg,
        gradedFeedbacksCount: cnt[i]
      });
    }
    return result;
  }

  private addMonthsUtc(d: Date, m: number): Date {
    const year = d.getUTCFullYear();
    const month = d.getUTCMonth();
    const day = d.getUTCDate();
    const nd = new Date(Date.UTC(year, month + m, 1));
    const lastDay = new Date(Date.UTC(nd.getUTCFullYear(), nd.getUTCMonth() + 1, 0)).getUTCDate();
    const safeDay = Math.min(day, lastDay);
    return new Date(Date.UTC(nd.getUTCFullYear(), nd.getUTCMonth(), safeDay));
  }

  formatRange(a: string, b: string): string {
    const s = new Date(a);
    const e = new Date(b);
    const sd = `${String(s.getUTCDate()).padStart(2, '0')}.${String(s.getUTCMonth() + 1).padStart(2, '0')}.${s.getUTCFullYear()}`;
    const ed = `${String(e.getUTCDate()).padStart(2, '0')}.${String(e.getUTCMonth() + 1).padStart(2, '0')}.${e.getUTCFullYear()}`;
    return `${sd}–${ed}`;
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
