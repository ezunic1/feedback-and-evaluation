import { Component } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Auth, RegisterRequest } from '../../services/auth';
import { Spinner } from '../../shared/spinner/spinner';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [RouterLink, CommonModule, FormsModule, Spinner],
  templateUrl: './register.html',
  styleUrls: ['./register.css']
})
export class Register {
  model: RegisterRequest = { fullName: '', email: '', password: '' };
  confirmPassword = '';
  message = '';
  loading = false;

  constructor(private auth: Auth, private router: Router) {}

  onSubmit() {
    if (this.loading) return;

    if (!this.model.fullName || !this.model.email || !this.model.password || !this.confirmPassword) {
      this.message = 'Please fill in all fields.';
      return;
    }
    if (this.model.password !== this.confirmPassword) {
      this.message = 'Passwords do not match.';
      return;
    }

    this.loading = true;
    this.message = '';

    this.auth.register(this.model).subscribe({
      next: (_res: any) => {
        this.loading = false;
        this.message = 'Registration successful! Redirecting to login...';
        setTimeout(() => this.router.navigate(['/login']), 1200);
      },
      error: (err: any) => {
        this.loading = false;
        this.message = err?.message || 'Registration failed.';
      }
    });
  }
}
