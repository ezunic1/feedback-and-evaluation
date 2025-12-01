import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Users, CreateUserRequest, Role, UserDto } from '../../services/users';
import { ServerErrors } from '../../shared/server-errors/server-errors';
import { ProblemDetails } from '../../models/problem-details';

@Component({
  selector: 'app-user-create',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, ServerErrors],
  templateUrl: './user-create.html',
  styleUrls: ['./user-create.css']
})
export class UserCreatePage {
  private router = inject(Router);
  private users = inject(Users);

  roles: Role[] = ['guest', 'intern', 'mentor', 'admin'];
  roleLabel: Record<Role, string> = { guest: 'Guest', intern: 'Intern', mentor: 'Mentor', admin: 'Admin' };

  model = { fullName: '', email: '', desc: '', role: 'guest' as Role, password: '', confirmPassword: '', forcePasswordChange: true };
  loading = false;
  problem: ProblemDetails | null = null;

  onSubmit(): void {
    this.problem = null;
    this.loading = true;

    const req: CreateUserRequest = {
      fullName: this.model.fullName.trim(),
      email: this.model.email.trim(),
      desc: this.model.desc?.trim() || null,
      seasonId: null,
      roleName: this.model.role,
      password: this.model.password,
      forcePasswordChange: this.model.forcePasswordChange
    };

    this.users.create(req).subscribe({
      next: (_: UserDto) => { this.loading = false; this.router.navigate(['/dashboard/admin']); },
      error: (err: ProblemDetails) => { this.loading = false; this.problem = err; }
    });
  }
}
