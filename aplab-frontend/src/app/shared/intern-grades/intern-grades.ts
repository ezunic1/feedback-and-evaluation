import { Component, Input, OnChanges, OnInit, SimpleChanges, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Spinner } from '../../shared/spinner/spinner';
import { Auth } from '../../services/auth';
import { Seasons, SeasonDto } from '../../services/seasons';
import { Users, UserListItem } from '../../services/users';
import { Feedbacks, FeedbackDto, MentorMonthlyAveragesDto, MentorMonthlyAveragesPageDto } from '../../services/feedbacks';

type DashRole = 'admin' | 'mentor' | 'other';
type MonthSlot = { index: number; start: Date; end: Date; label: string };
type Row = { id: string; name: string; avg: number | null };

@Component({
  selector: 'app-intern-grades',
  standalone: true,
  imports: [CommonModule, FormsModule, Spinner],
  templateUrl: './intern-grades.html',
  styleUrls: ['./intern-grades.css']
})
export class InternGrades implements OnInit, OnChanges {
  private auth = inject(Auth);
  private seasonsApi = inject(Seasons);
  private usersApi = inject(Users);
  private feedbacksApi = inject(Feedbacks);

  @Input() seasonId: number | null = null;

  role: DashRole = 'other';
  season: SeasonDto | null = null;
  months: MonthSlot[] = [];
  selectedMonthIndex = 1;

  loading = true;
  loadingAverages = false;

  allInterns: UserListItem[] = [];
  rows: Row[] = [];

  page = 1;
  pageSize = 10;
  totalPages = 0;
  pages: number[] = [];

  sortBy: 'name' | 'grade' = 'name';
  sortDir: 'asc' | 'desc' = 'asc';

  private mentorServerPaging = false;

  ngOnInit(): void {
    this.resolveRole();
    this.bootstrap();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['seasonId'] && !changes['seasonId'].firstChange) {
      this.bootstrap();
    }
  }

  private resolveRole(): void {
    if (this.auth.hasRole('admin')) { this.role = 'admin'; return; }
    if (this.auth.hasRole('mentor')) { this.role = 'mentor'; return; }
    this.role = 'other';
  }

  private bootstrap(): void {
    this.loading = true;
    this.mentorServerPaging = false;
    if (!this.seasonId) {
      this.season = null;
      this.months = [];
      this.rows = [];
      this.finishLoading(true);
      return;
    }
    this.seasonsApi.getAll().subscribe({
      next: ss => {
        const s = (ss || []).find(x => Number((x as any).id) === Number(this.seasonId)) ?? null;
        this.season = s ?? null;
        this.computeMonths();
        this.selectedMonthIndex = 1;
        if (this.role === 'mentor') {
          this.fetchAveragesForSelectedMonth();
        } else {
          this.loadInternsThenAveragesForAdmin();
        }
      },
      error: () => {
        this.season = null;
        this.months = [];
        this.rows = [];
        this.finishLoading(true);
      }
    });
  }

  private computeMonths(): void {
    this.months = [];
    if (!this.season) return;
    const sraw = (this.season as any).startDateUtc ?? (this.season as any).startDate;
    const eraw = (this.season as any).endDateUtc ?? (this.season as any).endDate;
    const start = new Date(String(sraw));
    const end0 = new Date(String(eraw));
    if (!(start instanceof Date) || !(end0 instanceof Date) || isNaN(start.getTime()) || isNaN(end0.getTime()) || end0 <= start) return;
    const today = new Date();
    const end = end0 < today ? end0 : today;
    let cur = new Date(Date.UTC(start.getUTCFullYear(), start.getUTCMonth(), start.getUTCDate()));
    let idx = 1;
    while (cur < end) {
      const next = this.addMonthsUtc(cur, 1);
      const slotEnd = next < end ? next : end;
      this.months.push({ index: idx, start: cur, end: slotEnd, label: `Month ${idx}` });
      cur = next;
      idx++;
    }
    if (this.months.length === 0) {
      this.months.push({ index: 1, start: start, end: end, label: 'Month 1' });
    }
  }

  private addMonthsUtc(d: Date, m: number): Date {
    const y = d.getUTCFullYear();
    const mo = d.getUTCMonth();
    const day = d.getUTCDate();
    const nd = new Date(Date.UTC(y, mo + m, 1));
    const lastDay = new Date(Date.UTC(nd.getUTCFullYear(), nd.getUTCMonth() + 1, 0)).getUTCDate();
    const safe = Math.min(day, lastDay);
    return new Date(Date.UTC(nd.getUTCFullYear(), nd.getUTCMonth(), safe));
  }

  private loadInternsThenAveragesForAdmin(): void {
    this.usersApi.getBySeason(this.seasonId!).subscribe({
      next: us => {
        this.allInterns = (us || []).filter(u => u.role === 'intern');
        this.fetchAveragesForSelectedMonth();
      },
      error: () => {
        this.allInterns = [];
        this.fetchAveragesForSelectedMonth();
      }
    });
  }

  onMonthChange(): void {
    this.page = 1;
    this.fetchAveragesForSelectedMonth();
  }

  private fetchAveragesForSelectedMonth(): void {
    this.loadingAverages = true;
    if (!this.seasonId) {
      this.rows = [];
      this.finishLoading(true);
      return;
    }

    if (this.role === 'mentor') {
      this.feedbacksApi.getMentorMonthlyAveragesPaged(this.seasonId, this.selectedMonthIndex, {
        sortBy: this.sortBy,
        sortDir: this.sortDir,
        page: this.page,
        pageSize: this.pageSize
      }).subscribe({
        next: (dto: MentorMonthlyAveragesPageDto) => {
          this.mentorServerPaging = true;
          this.totalPages = dto.totalPages || 1;
          this.pages = Array.from({ length: this.totalPages }, (_, i) => i + 1);
          this.rows = (dto.items || []).map(it => {
            const name = (it.fullName || '').trim() || (it.email || '').trim() || it.internUserId;
            return { id: it.internUserId, name, avg: it.averageScore ?? null };
          });
          this.visible = this.rows.slice(0, this.rows.length);
          this.finishLoading(false);
        },
        error: () => {
          this.mentorServerPaging = false;
          this.feedbacksApi.getMentorMonthlyAverages(this.seasonId!, this.selectedMonthIndex).subscribe({
            next: (dto: MentorMonthlyAveragesDto) => {
              this.rows = (dto.items || []).map(it => ({
                id: it.internUserId,
                name: it.internUserId,
                avg: it.averageScore ?? null
              }));
              this.finishLoading(true);
            },
            error: () => {
              this.rows = [];
              this.finishLoading(true);
            }
          });
        }
      });
      return;
    }

    if (this.role === 'admin') {
      this.fetchAllAdminFeedbacks(this.seasonId, 1, []).then(list => {
        const slot = this.months.find(m => m.index === this.selectedMonthIndex);
        const start = slot?.start ?? new Date(0);
        const end = slot?.end ?? new Date(8640000000000000);
        const graded = (list || []).filter(f => !!f.grade && this.inRange(new Date(f.createdAtUtc), start, end));
        const byIntern = new Map<string, { sum: number; n: number }>();
        for (const f of graded) {
          const g = f.grade!;
          const avg = (g.careerSkills + g.communication + g.collaboration) / 3;
          const rid = f.receiverUserId;
          if (!byIntern.has(rid)) byIntern.set(rid, { sum: 0, n: 0 });
          const s = byIntern.get(rid)!;
          s.sum += avg;
          s.n += 1;
        }
        this.rows = this.allInterns.map(i => {
          const agg = byIntern.get(i.id) || null;
          const avg = agg ? (agg.sum / agg.n) : null;
          return { id: i.id, name: (i.fullName ?? '').trim() || i.email, avg };
        });
        this.finishLoading(true);
      }).catch(() => {
        this.rows = this.allInterns.map(i => ({ id: i.id, name: (i.fullName ?? '').trim() || i.email, avg: null }));
        this.finishLoading(true);
      });
      return;
    }

    this.rows = [];
    this.finishLoading(true);
  }

  private inRange(t: Date, a: Date, b: Date): boolean {
    return t >= a && t < b;
  }

  private fetchAllAdminFeedbacks(seasonId: number, page: number, acc: FeedbackDto[], guard: number = 0): Promise<FeedbackDto[]> {
    return new Promise((resolve, reject) => {
      if (guard > 200) { resolve(acc); return; }
      this.feedbacksApi.getForAdmin(page, seasonId).subscribe({
        next: arr => {
          const list = arr || [];
          const nextAcc = acc.concat(list);
          if (list.length === 0) resolve(nextAcc);
          else this.fetchAllAdminFeedbacks(seasonId, page + 1, nextAcc, guard + 1).then(resolve).catch(reject);
        },
        error: () => resolve(acc)
      });
    });
  }

  private finishLoading(useClientPaging: boolean): void {
    this.loadingAverages = false;
    this.loading = false;
    if (useClientPaging) this.applySortAndPaginate();
  }

  changeSort(field: 'name' | 'grade'): void {
    if (field === 'grade') {
      if (this.sortBy === 'grade') this.sortDir = this.sortDir === 'asc' ? 'desc' : 'asc';
      else { this.sortBy = 'grade'; this.sortDir = 'desc'; }
    } else {
      if (this.sortBy === 'name') this.sortDir = this.sortDir === 'asc' ? 'desc' : 'asc';
      else { this.sortBy = 'name'; this.sortDir = 'asc'; }
    }
    this.page = 1;
    if (this.role === 'mentor' && this.mentorServerPaging) {
      this.fetchAveragesForSelectedMonth();
      return;
    }
    this.applySortAndPaginate();
  }

  private applySortAndPaginate(): void {
    const sorted = [...this.rows].sort((a, b) => {
      if (this.sortBy === 'name') {
        const cmp = a.name.localeCompare(b.name);
        return this.sortDir === 'asc' ? cmp : -cmp;
      }
      const av = a.avg; const bv = b.avg;
      if (av == null && bv == null) return 0;
      if (av == null) return 1;
      if (bv == null) return -1;
      const cmp = av - bv;
      return this.sortDir === 'asc' ? cmp : -cmp;
    });
    this.totalPages = Math.max(1, Math.ceil(sorted.length / this.pageSize));
    this.pages = Array.from({ length: this.totalPages }, (_, i) => i + 1);
    const start = (this.page - 1) * this.pageSize;
    this.visible = sorted.slice(start, start + this.pageSize);
  }

  visible: Row[] = [];

  pageClick(p: number): void {
    if (p === this.page) return;
    this.page = p;
    if (this.role === 'mentor' && this.mentorServerPaging) {
      this.fetchAveragesForSelectedMonth();
      return;
    }
    this.applySortAndPaginate();
  }
}
