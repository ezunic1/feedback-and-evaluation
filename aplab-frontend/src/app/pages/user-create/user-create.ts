import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Users, CreateUserRequest, Role, UserDto } from '../../services/users';

@Component({
  selector: 'app-user-create',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './user-create.html',
  styleUrls: ['./user-create.css']
})
export class UserCreatePage {
  private router = inject(Router);
  private users = inject(Users);

  roles: Role[] = ['guest', 'intern', 'mentor', 'admin'];
  roleLabel: Record<Role, string> = {
    guest: 'Guest',
    intern: 'Intern',
    mentor: 'Mentor',
    admin: 'Admin'
  };

  model = {
    fullName: '',
    email: '',
    desc: '',
    role: 'guest' as Role,
    password: '',
    confirmPassword: '',
    forcePasswordChange: true
  };

  loading = false;
  message = '';

  onSubmit(): void {
    this.message = '';

    if (!this.model.fullName || !this.model.email || !this.model.password || !this.model.confirmPassword) {
      this.message = 'Please fill all required fields.';
      return;
    }
    if (this.model.password !== this.model.confirmPassword) {
      this.message = 'Passwords do not match.';
      return;
    }

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
      next: (_: UserDto) => {
        this.loading = false;
        this.router.navigate(['/dashboard/admin']);
      },
      error: () => {
        this.loading = false;
        this.message = 'Failed to create user.';
      }
    });
  }
}
