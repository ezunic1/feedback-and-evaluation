import { Component } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule, NgForm } from '@angular/forms';
import { Auth, LoginRequest } from '../../services/auth';
import { Spinner } from '../../shared/spinner/spinner';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [RouterLink, CommonModule, FormsModule, Spinner],
  templateUrl: './login.html',
  styleUrl: './login.css'
})
export class Login {
  model: LoginRequest = { usernameOrEmail: '', password: '' };
  loading = false;
  message = '';
  isError = false;

  changeRequired = false;
  changeUrl = '';
  changeMsg = 'You must change your password before the first login.';

  constructor(private auth: Auth, private router: Router) {}

  onSubmit(form: NgForm) {
    if (this.loading) return;

    const raw = (this.model.usernameOrEmail || '').trim();
    const pass = (this.model.password || '').trim();

    if (!raw || !pass || form.invalid) {
      form.form.markAllAsTouched();
      this.message = 'Please fix the errors above.';
      this.isError = true;
      return;
    }

    const usernameOrEmail = raw.includes('@') ? raw.toLowerCase() : raw;

    this.loading = true;
    this.message = '';
    this.isError = false;
    this.changeRequired = false;
    this.changeUrl = '';

    this.auth.login({ usernameOrEmail, password: pass }).subscribe({
      next: () => {
        this.loading = false;
        this.isError = false;
        this.router.navigateByUrl('/dashboard');
      },
      error: (err: any) => {
        this.loading = false;

        if (err && err.changeRequired) {
          this.changeRequired = true;
          this.changeUrl = err.url || '';
          this.changeMsg = err.message || this.changeMsg;
          this.message = '';
          this.isError = false;
          return;
        }

        const detail = (err?.error && typeof err.error === 'object') ? (err.error.detail || err.error.message) : null;
        const str = typeof err?.error === 'string' ? err.error : null;
        this.message = err?.message || detail || str || 'Login failed.';
        this.isError = true;
        console.error('[Login] error', err);
      }
    });
  }
}
