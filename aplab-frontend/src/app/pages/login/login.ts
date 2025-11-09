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

  changeRequired = false;
  changeUrl = '';
  changeMsg = 'You must change your password before the first login.';

  constructor(private auth: Auth, private router: Router) {}

  onSubmit() {
    if (!this.model.usernameOrEmail || !this.model.password) {
      this.message = 'Please enter email/username and password.';
      return;
    }
    this.loading = true;
    this.message = '';
    this.changeRequired = false;
    this.changeUrl = '';

    this.auth.login(this.model).subscribe({
      next: () => {
        this.loading = false;
        this.router.navigateByUrl('/dashboard');
      },
      error: (err: any) => {
        this.loading = false;
        if (err && err.changeRequired) {
          this.changeRequired = true;
          this.changeUrl = err.url || '';
          this.changeMsg = err.message || this.changeMsg;
          this.message = '';
          return;
        }
        this.message = err?.message || 'Login failed.';
        console.error('[Login] error', err);
      }
    });
  }
}
