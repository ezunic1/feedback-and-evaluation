import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Seasons, CreateSeasonRequest, SeasonDto } from '../../services/seasons';
import { Users, UserListItem } from '../../services/users';

@Component({
  selector: 'app-season-create',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './season-create.html',
  styleUrl: './season-create.css'
})
export class SeasonCreatePage implements OnInit {
  private router = inject(Router);
  private seasons = inject(Seasons);
  private users = inject(Users);

  model = {
    name: '',
    startDate: '',
    endDate: '',
    mentorId: ''
  };

  mentors: UserListItem[] = [];
  loadingMentors = true;
  loading = false;
  message = '';

  ngOnInit(): void {
    this.users.getMentors(200).subscribe({
      next: r => { this.mentors = r; this.loadingMentors = false; },
      error: () => { this.mentors = []; this.loadingMentors = false; }
    });
  }

  onSubmit(): void {
    if (!this.model.name || !this.model.startDate || !this.model.endDate) {
      this.message = 'Please fill all required fields.';
      return;
    }
    const sd = new Date(this.model.startDate + 'T00:00:00Z');
    const ed = new Date(this.model.endDate + 'T00:00:00Z');
    if (sd >= ed) {
      this.message = 'Start date must be before end date.';
      return;
    }
    this.loading = true;
    this.message = '';
    const req: CreateSeasonRequest = {
      name: this.model.name.trim(),
      startDate: sd.toISOString(),
      endDate: ed.toISOString(),
      mentorId: this.model.mentorId ? this.model.mentorId : null
    };
    this.seasons.create(req).subscribe({
      next: (_: SeasonDto) => {
        this.loading = false;
        this.router.navigate(['/']);
      },
      error: (err) => {
        this.loading = false;
        this.message = 'Failed to create season.';
      }
    });
  }
}
