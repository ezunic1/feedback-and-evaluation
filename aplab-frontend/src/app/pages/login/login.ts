import { Component } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule, NgForm } from '@angular/forms';
import { Auth, LoginRequest } from '../../services/auth';
import { Spinner } from '../../shared/spinner/spinner';
import { ServerErrors } from '../../shared/server-errors/server-errors';
import { ProblemDetails } from '../../models/problem-details';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [RouterLink, CommonModule, FormsModule, Spinner, ServerErrors],
  templateUrl: './login.html',
  styleUrl: './login.css'
})
export class Login {
  model: LoginRequest = { usernameOrEmail: '', password: '' };
  loading = false;
  problem: ProblemDetails | null = null;
  changeRequired = false;
  changeUrl = '';
  changeMsg = 'You must change your password before the first login.';

  constructor(private auth: Auth, private router: Router) {}

  onSubmit(form: NgForm) {
    if (this.loading) return;

    const raw = (this.model.usernameOrEmail || '').trim();
    const pass = (this.model.password || '').trim();
    if (!raw || !pass || form.invalid) {
      this.problem = { title: 'Error', detail: 'Please fill in both fields.' };
      return;
    }

    const usernameOrEmail = raw.includes('@') ? raw.toLowerCase() : raw;

    this.loading = true;
    this.problem = null;
    this.changeRequired = false;
    this.changeUrl = '';

    this.auth.login({ usernameOrEmail, password: pass }).subscribe({
      next: () => {
        this.loading = false;
        this.router.navigateByUrl('/dashboard');
      },
      error: (err: any) => {
        this.loading = false;
        if (err?.changeRequired) {
          this.changeRequired = true;
          this.changeUrl = err.url || '';
          this.changeMsg = err.message || this.changeMsg;
          this.problem = null;
          return;
        }
        this.problem = err as ProblemDetails;
      }
    });
  }
}
