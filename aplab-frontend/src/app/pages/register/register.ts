import { Component } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule, NgForm } from '@angular/forms';
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

  onSubmit(form: NgForm) {
    if (this.loading) return;

    const fullName = (this.model.fullName || '').trim();
    const email = (this.model.email || '').trim().toLowerCase();
    const password = this.model.password || '';
    const confirm = this.confirmPassword || '';

    if (!fullName || !email || !password || !confirm || confirm !== password || form.invalid) {
      form.form.markAllAsTouched();
      this.message = confirm && password && confirm !== password ? 'Passwords do not match.' : 'Please fix the errors above.';
      return;
    }

    this.loading = true;
    this.message = '';

    this.auth.register({ fullName, email, password }).subscribe({
      next: () => {
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
