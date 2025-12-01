import { Component } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule, NgForm } from '@angular/forms';
import { Auth, RegisterRequest } from '../../services/auth';
import { Spinner } from '../../shared/spinner/spinner';
import { ServerErrors } from '../../shared/server-errors/server-errors';
import { ProblemDetails } from '../../models/problem-details';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [RouterLink, CommonModule, FormsModule, Spinner, ServerErrors],
  templateUrl: './register.html',
  styleUrls: ['./register.css']
})
export class Register {
  model: RegisterRequest = { fullName: '', email: '', password: '' };
  confirmPassword = '';
  loading = false;
  problem: ProblemDetails | null = null;
  successMsg = '';

  constructor(private auth: Auth, private router: Router) {}

  onSubmit(form: NgForm) {
    if (this.loading) return;
    this.loading = true;
    this.problem = null;
    this.successMsg = '';

    this.auth.register({
      fullName: (this.model.fullName || '').trim(),
      email: (this.model.email || '').trim().toLowerCase(),
      password: this.model.password || ''
    }).subscribe({
      next: () => {
        this.loading = false;
        this.successMsg = 'Registration successful! Redirecting to login...';
        setTimeout(() => this.router.navigate(['/login']), 1200);
      },
      error: (err: ProblemDetails) => {
        this.loading = false;
        this.problem = err;
      }
    });
  }
}
