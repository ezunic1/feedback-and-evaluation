import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { Auth } from '../../../services/auth';

@Component({
  selector: 'app-dashboard-redirect',
  standalone: true,
  imports: [CommonModule],
  template: ''
})
export class DashboardRedirect implements OnInit {
  private router = inject(Router);
  private auth = inject(Auth);

  ngOnInit(): void {
    if (!this.auth.isLoggedIn()) { this.router.navigate(['/login']); return; }
    const role = (this.auth.role() || 'guest').toLowerCase();
    if (role === 'admin') this.router.navigate(['/dashboard/admin']);
    else if (role === 'mentor') this.router.navigate(['/dashboard/mentor']);
    else if (role === 'intern') this.router.navigate(['/dashboard/intern']);
    else this.router.navigate(['/']);
  }
}
