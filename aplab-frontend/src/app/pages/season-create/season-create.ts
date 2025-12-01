import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Seasons, CreateSeasonRequest, SeasonDto } from '../../services/seasons';
import { Users, UserListItem } from '../../services/users';
import { ServerErrors } from '../../shared/server-errors/server-errors';
import { ProblemDetails } from '../../models/problem-details';

@Component({
  selector: 'app-season-create',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, ServerErrors],
  templateUrl: './season-create.html',
  styleUrls: ['./season-create.css']
})
export class SeasonCreatePage implements OnInit {
  private router = inject(Router);
  private seasons = inject(Seasons);
  private users = inject(Users);

  model = { name: '', startDate: '', endDate: '', mentorId: '' };
  mentors: UserListItem[] = [];
  loadingMentors = true;
  loading = false;
  problem: ProblemDetails | null = null;

  ngOnInit(): void {
    this.users.getMentors(200).subscribe({
      next: r => { this.mentors = r; this.loadingMentors = false; },
      error: () => { this.mentors = []; this.loadingMentors = false; }
    });
  }

  onSubmit(): void {
    this.problem = null;
    this.loading = true;

    const sd = new Date(this.model.startDate + 'T00:00:00Z');
    const ed = new Date(this.model.endDate + 'T00:00:00Z');

    const req: CreateSeasonRequest = {
      name: this.model.name.trim(),
      startDate: sd.toISOString(),
      endDate: ed.toISOString(),
      mentorId: this.model.mentorId ? this.model.mentorId : null
    };

    this.seasons.create(req).subscribe({
      next: (_: SeasonDto) => { this.loading = false; this.router.navigate(['/dashboard/admin']); },
      error: (err: ProblemDetails) => { this.loading = false; this.problem = err; }
    });
  }
}
