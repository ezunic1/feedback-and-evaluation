import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { Auth } from '../../services/auth';
import { Navbar } from '../../shared/navbar/navbar';

@Component({
  selector: 'app-landing',
  standalone: true,
  imports: [CommonModule, RouterLink, Navbar],
  templateUrl: './landing.html',
  styleUrl: './landing.css'
})
export class Landing {
  constructor(public auth: Auth, private router: Router) {}

  get isLoggedIn(): boolean {
    return this.auth.isLoggedIn();
  }

  logout() {
    this.auth.logout();
    this.router.navigateByUrl('/');
  }
}
