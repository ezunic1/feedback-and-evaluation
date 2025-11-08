import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { Auth } from '../../services/auth';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './navbar.html',
  styleUrls: ['./navbar.css']
})
export class Navbar {
  private router = inject(Router);
  auth = inject(Auth);

  private hiddenOn = ['/login', '/register'];

  visible() {
    const url = this.router.url || '/';
    return !this.hiddenOn.some(p => url.startsWith(p));
  }

  logout() {
    this.auth.logout();
    this.router.navigateByUrl('/');
  }
}
