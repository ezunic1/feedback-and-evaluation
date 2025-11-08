import { Component, OnInit, inject } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { PLATFORM_ID } from '@angular/core';
import { Auth } from '../../services/auth';
import { ProfileService, MeResponse } from '../../services/profile';
import { Navbar } from '../../shared/navbar/navbar';

type Role = 'guest' | 'intern' | 'mentor' | 'admin' | 'unknown';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, Navbar],
  templateUrl: './profile.html',
  styleUrls: ['./profile.css']
})
export class Profile implements OnInit {
  private platformId = inject(PLATFORM_ID);

  displayName?: string;
  email?: string;
  role: Role = 'unknown';
  description: string | null = null;
  internSeasonName: string | null = null;
  mentorSeasonName: string | null = null;
  loading = true;

  isEditing = false;
  saving = false;
  fullNameInput = '';
  descInput: string | null = null;

  constructor(public auth: Auth, private router: Router, private api: ProfileService) {}

  async ngOnInit() {
    if (!this.auth.isLoggedIn()) {
      this.router.navigate(['/login']);
      return;
    }

    this.displayName = this.auth.user()?.name || undefined;
    this.email = this.auth.user()?.email || undefined;

    if (!isPlatformBrowser(this.platformId)) return;

    const me: MeResponse | null = await this.api.getMe();
    if (me) {
      this.displayName = me.name ?? this.displayName;
      this.email = me.email ?? this.email;
      this.role = this.normalizeRole(me.role);
      this.internSeasonName = me.internSeasonName ?? null;
      this.mentorSeasonName = me.mentorSeasonName ?? null;
      this.description = me.description ?? null;
    }
    this.loading = false;
  }

  edit() {
    this.fullNameInput = this.displayName || '';
    this.descInput = this.description ?? null;
    this.isEditing = true;
  }

  cancel() {
    this.isEditing = false;
  }

  async save() {
    if (!this.fullNameInput?.trim()) return;
    this.saving = true;
    try {
      const body = { fullName: this.fullNameInput.trim(), description: this.descInput ?? null };
      const updated = await this.api.updateMe(body);
      this.displayName = updated.name ?? body.fullName;
      this.description = updated.description ?? body.description ?? null;
      this.isEditing = false;                       // “vrati” na prikaz profila
    } finally {
      this.saving = false;
    }
  }

  private normalizeRole(r?: string): Role {
    if (!r) return 'unknown';
    const x = r.toLowerCase();
    if (x.includes('admin')) return 'admin';
    if (x.includes('mentor')) return 'mentor';
    if (x.includes('intern')) return 'intern';
    if (x.includes('guest')) return 'guest';
    return 'unknown';
  }
}
