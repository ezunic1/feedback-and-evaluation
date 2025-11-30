import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Navbar } from '../../shared/navbar/navbar';
import { Seasons, SeasonDto } from '../../services/seasons';
import { Feedbacks, FeedbackDto, PagedResult, MonthSpanDto } from '../../services/feedbacks';
import { Users, UserListItem, Role } from '../../services/users';
import { Auth } from '../../services/auth';
import { Spinner } from '../../shared/spinner/spinner';
import { RequestDelete } from '../../shared/request-delete/request-delete';
import { ConfirmDelete } from '../../shared/confirm-delete/confirm-delete';

type DashRole = 'admin' | 'mentor' | 'other';
type SeasonVM = { id: number; name: string; startDateUtc?: string | null; endDateUtc?: string | null; mentorUserId?: string | null; mentorName?: string | null; };
type FeedbackType = 'all' | 'i2i' | 'i2m' | 'm2i';

@Component({
  selector: 'app-all-feedbacks',
  standalone: true,
  imports: [CommonModule, FormsModule, Navbar, Spinner, RequestDelete, ConfirmDelete],
  templateUrl: './all-feedbacks.html',
  styleUrls: ['./all-feedbacks.css']
})
export class AllFeedbacks implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private seasonsApi = inject(Seasons);
  private feedbackApi = inject(Feedbacks);
  private usersApi = inject(Users);
  private auth = inject(Auth);

  role: DashRole = 'other';
  seasonsVm: SeasonVM[] = [];
  selectedSeasonId: number | null = null;

  type: FeedbackType = 'all';
  sortDir: 'asc' | 'desc' = 'desc';

  months: MonthSpanDto[] = [];
  selectedMonth: number | 'all' = 'all';

  loading = true;
  loadingUsers = true;

  rows: FeedbackDto[] = [];
  page = 1;
  readonly pageSize = 10;
  total = 0;
  totalPages = 0;
  pages: number[] = [];

  private usersById = new Map<string, { name: string; role: Role | null }>();
  private expanded = new Set<number>();

  openedRequestId: number | null = null;

  confirmOpen = false;
  confirmLoading = false;
  confirmTargetId: number | null = null;
  confirmTitle = 'Delete feedback';
  confirmMessage = 'Are you sure you want to permanently delete this feedback?';
  confirmLabel = 'Delete';
  cancelLabel = 'Cancel';

  ngOnInit(): void {
    this.resolveRole();
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
        this.initFromUrl();
      },
      error: () => {
        this.seasonsVm = [];
        this.selectedSeasonId = null;
        this.loading = false;
      }
    });
  }

  onSeasonChange(): void {
    this.page = 1;
    this.selectedMonth = 'all';
    this.pushUrl();
    this.loadUsersForSelectedSeason();
    this.loadMonthsForSeason();
    this.loadPage();
  }

  onTypeChange(): void {
    this.page = 1;
    this.pushUrl();
    this.loadPage();
  }

  onMonthChange(): void {
    this.page = 1;
    this.pushUrl();
    this.loadPage();
  }

  changeSortDate(): void {
    this.sortDir = this.sortDir === 'asc' ? 'desc' : 'asc';
    this.page = 1;
    this.pushUrl();
    this.loadPage();
  }

  pageClick(p: number): void {
    if (p === this.page) return;
    this.page = p;
    this.pushUrl();
    this.loadPage();
  }

  toggle(id: number): void {
    if (this.expanded.has(id)) this.expanded.delete(id);
    else this.expanded.add(id);
  }

  isExpanded(id: number): boolean {
    return this.expanded.has(id);
  }

  hasGrades(f: FeedbackDto): boolean {
    return !!f.grade;
  }

  nameOf(id: string | null | undefined): string {
    if (!id) return '';
    const hit = this.usersById.get(id);
    if (hit) return hit.name;
    const s = this.currentSeasonVm();
    if (s && s.mentorUserId === id) return s.mentorName || 'Mentor';
    return '';
  }

  openRequest(feedbackId: number, ev: Event) {
    ev.stopPropagation();
    this.openedRequestId = feedbackId;
  }

  onRequestClosed(submitted: boolean) {
    this.openedRequestId = null;
    if (submitted) {
      this.loadPage();
    }
  }

  openDeleteConfirm(id: number, ev: Event) {
    ev.stopPropagation();
    if (this.role !== 'admin') return;
    this.confirmTargetId = id;
    this.confirmOpen = true;
    this.confirmLoading = false;
  }

  onCancelDelete() {
    if (this.confirmLoading) return;
    this.confirmOpen = false;
    this.confirmTargetId = null;
  }

  onConfirmDelete() {
    if (!this.confirmTargetId || this.confirmLoading) return;
    this.confirmLoading = true;
    this.feedbackApi.delete(this.confirmTargetId).subscribe({
      next: () => {
        this.confirmLoading = false;
        this.confirmOpen = false;
        this.confirmTargetId = null;
        this.loadPage();
      },
      error: () => {
        this.confirmLoading = false;
        alert('Failed to delete feedback.');
      }
    });
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

  private initFromUrl(): void {
    const qp = this.route.snapshot.queryParamMap;
    const id = Number(qp.get('seasonId'));
    const t = (qp.get('type') as FeedbackType) || 'all';
    const sd = (qp.get('sort') as 'asc' | 'desc') || 'desc';
    const p = Number(qp.get('page'));
    const m = qp.get('month');

    if (!isNaN(id) && this.seasonsVm.some(s => s.id === id)) this.selectedSeasonId = id;
    else this.selectedSeasonId = this.seasonsVm.length ? this.seasonsVm[0].id : null;

    this.type = t === 'i2i' || t === 'i2m' || t === 'm2i' ? t : 'all';
    this.sortDir = sd === 'asc' ? 'asc' : 'desc';
    this.page = !isNaN(p) && p > 0 ? p : 1;

    if (m === null || m === 'all') this.selectedMonth = 'all';
    else {
      const mi = Number(m);
      this.selectedMonth = !isNaN(mi) && mi > 0 ? mi : 'all';
    }

    this.pushUrl(true);
    this.loadUsersForSelectedSeason();
    this.loadMonthsForSeason(() => this.loadPage());
  }

  private pushUrl(replace = false): void {
    const q: any = {
      seasonId: this.selectedSeasonId ?? undefined,
      type: this.type !== 'all' ? this.type : undefined,
      sort: this.sortDir !== 'desc' ? this.sortDir : undefined,
      page: this.page !== 1 ? this.page : undefined,
      month: this.selectedMonth !== 'all' ? this.selectedMonth : undefined
    };
    this.router.navigate([], { relativeTo: this.route, queryParams: q, queryParamsHandling: '', replaceUrl: replace });
  }

  private currentSeasonVm(): SeasonVM | undefined {
    if (this.selectedSeasonId == null) return undefined;
    return this.seasonsVm.find(s => s.id === this.selectedSeasonId);
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

  private loadMonthsForSeason(done?: () => void): void {
    if (!this.selectedSeasonId) { this.months = []; if (done) done(); return; }
    this.feedbackApi.getSeasonMonths(this.selectedSeasonId).subscribe({
      next: ms => {
        this.months = ms || [];
        if (this.selectedMonth !== 'all') {
          const exists = this.months.some(x => x.index === this.selectedMonth);
          if (!exists) this.selectedMonth = 'all';
        }
        if (done) done();
      },
      error: () => {
        this.months = [];
        this.selectedMonth = 'all';
        if (done) done();
      }
    });
  }

  private loadPage(): void {
    if (!this.selectedSeasonId) {
      this.rows = [];
      this.total = 0;
      this.totalPages = 0;
      this.pages = [];
      this.loading = false;
      return;
    }
    this.loading = true;
    this.feedbackApi.search(this.selectedSeasonId, {
      type: this.type,
      sortDir: this.sortDir,
      page: this.page,
      pageSize: this.pageSize,
      monthIndex: this.selectedMonth === 'all' ? undefined : Number(this.selectedMonth)
    }).subscribe({
      next: (res: PagedResult<FeedbackDto>) => {
        this.rows = res.items || [];
        this.page = res.page;
        this.total = res.total;
        this.totalPages = res.totalPages;
        this.pages = Array.from({ length: this.totalPages || 0 }, (_, i) => i + 1);
        this.loading = false;
        this.expanded.clear();
      },
      error: () => {
        this.rows = [];
        this.total = 0;
        this.totalPages = 0;
        this.pages = [];
        this.loading = false;
        this.expanded.clear();
      }
    });
  }
}
