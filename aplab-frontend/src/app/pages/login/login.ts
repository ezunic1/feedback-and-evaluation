import { Component } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Auth, LoginRequest } from '../../services/auth';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [RouterLink, CommonModule, FormsModule],
  templateUrl: './login.html',
  styleUrl: './login.css'
})
export class Login {
  model: LoginRequest = { usernameOrEmail: '', password: '' };
  loading = false;
  message = '';

  constructor(private auth: Auth, private router: Router) {}

  onSubmit() {
    if (!this.model.usernameOrEmail || !this.model.password) {
      this.message = 'Please enter email/username and password.';
      return;
    }
    this.loading = true;
    this.message = '';

    this.auth.login(this.model).subscribe({
      next: () => {
        this.loading = false;
        this.router.navigateByUrl('/');
      },
      error: (err: any) => {
        this.loading = false;
        this.message = err?.message || 'Login failed.';
        console.error('[Login] error', err);
      }
    });
  }
}
