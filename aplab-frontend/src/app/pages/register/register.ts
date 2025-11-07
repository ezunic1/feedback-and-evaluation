import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Auth, RegisterRequest } from '../../services/auth';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [RouterLink, CommonModule, FormsModule],
  templateUrl: './register.html',
  styleUrl: './register.css'
})
export class Register {
  model: RegisterRequest = { fullName: '', email: '', password: '' };
  confirmPassword = '';
  message = '';
  loading = false;

  constructor(private auth: Auth) {}

  onSubmit() {
  console.log('[Register] submit clicked');
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
      console.log('[Register] success', _res);
      this.message = 'Registration successful! You can now log in.';
      this.loading = false;
    },
    error: (err: any) => {
      console.error('[Register] error', err);
      this.message = err?.message || 'Registration failed.';
      this.loading = false;
    }
  });
}

}
