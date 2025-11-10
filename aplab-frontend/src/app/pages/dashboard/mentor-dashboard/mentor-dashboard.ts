import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { Navbar } from '../../../shared/navbar/navbar';
import { SeasonList } from '../../../shared/season-list/season-list';
import { Seasons, SeasonDto } from '../../../services/seasons';
import { Users, UserListItem } from '../../../services/users';
import { Auth } from '../../../services/auth';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

type MeDto = { name?: string | null; email?: string | null };

@Component({
  selector: 'app-mentor-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, Navbar, SeasonList],
  templateUrl: './mentor-dashboard.html',
  styleUrl: './mentor-dashboard.css'
})
export class MentorDashboard implements OnInit {
  private router = inject(Router);
  private auth = inject(Auth);
  private http = inject(HttpClient);
  private seasonsApi = inject(Seasons);
  private usersApi = inject(Users);

  loading = true;
  all: SeasonDto[] = [];
  ongoing: SeasonDto | null = null;
  myPrevious: SeasonDto[] = [];
  others: SeasonDto[] = [];

  ngOnInit(): void {
    if (!this.auth.isLoggedIn()) { this.router.navigate(['/login']); return; }

    this.http.get<MeDto>('/api/users/me').pipe(
      catchError(() => of({ name: this.auth.user()?.name ?? null, email: this.auth.user()?.email ?? null }))
    ).subscribe(me => {
      const meName = (me?.name ?? '').toLowerCase().trim();
      const meEmail = (me?.email ?? '').toLowerCase().trim();
      const now = Date.now();

      forkJoin([
        this.seasonsApi.getAll(),
        this.usersApi.getMentors(500)
      ]).subscribe({
        next: ([seasons, mentors]) => {
          this.all = seasons ?? [];

          const myMentorRow: UserListItem | undefined =
            (mentors || []).find(m =>
              (m.email || '').toLowerCase().trim() === meEmail ||
              (m.fullName || '').toLowerCase().trim() === meName
            );

          const myLocalId = (myMentorRow?.id || '').toLowerCase().trim();

          const mine = this.all.filter(s => {
            const byId = myLocalId && (s.mentorId || '').toLowerCase().trim() === myLocalId;
            const byName = meName && (s.mentorName || '').toLowerCase().trim() === meName;
            return byId || byName;
          });

          const inRange = (s: SeasonDto) => {
            const sd = new Date(s.startDate).getTime();
            const ed = new Date(s.endDate).getTime();
            return sd <= now && now <= ed;
          };
          const isUpcoming = (s: SeasonDto) => new Date(s.startDate).getTime() > now;
          const isPast = (s: SeasonDto) => new Date(s.endDate).getTime() < now;

          const ongoingCandidates = mine.filter(inRange).sort((a, b) => new Date(b.startDate).getTime() - new Date(a.startDate).getTime());
          const upcomingCandidates = mine.filter(isUpcoming).sort((a, b) => new Date(a.startDate).getTime() - new Date(b.startDate).getTime());
          const pastCandidates = mine.filter(isPast).sort((a, b) => new Date(b.endDate).getTime() - new Date(a.endDate).getTime());

          this.ongoing = ongoingCandidates[0] ?? upcomingCandidates[0] ?? pastCandidates[0] ?? null;

          this.myPrevious = pastCandidates;

          this.others = this.all
            .filter(s => mine.every(ms => ms.id !== s.id))
            .sort((a, b) => new Date(a.startDate).getTime() - new Date(b.startDate).getTime());

          this.loading = false;
        },
        error: () => { this.loading = false; }
      });
    });
  }

  openSeason(idOrEvent: any) {
    const id = typeof idOrEvent === 'number' ? idOrEvent : Number(idOrEvent?.id ?? idOrEvent);
    if (!isNaN(id)) this.router.navigate(['/seasons', id]);
  }

  openOther(idOrEvent: any) {
    const id = typeof idOrEvent === 'number' ? idOrEvent : Number(idOrEvent?.id ?? idOrEvent);
    if (!isNaN(id)) this.router.navigate(['/seasons/view', id]);
  }
}
