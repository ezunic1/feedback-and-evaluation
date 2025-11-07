import { Component, OnInit, inject } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { PLATFORM_ID } from '@angular/core';
import { Auth } from '../../services/auth';
import { ProfileService, MeResponse } from '../../services/profile';

type Role = 'guest' | 'intern' | 'mentor' | 'admin' | 'unknown';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './profile.html',
  styleUrl: './profile.css'
})
export class Profile implements OnInit {
  private platformId = inject(PLATFORM_ID);

  displayName?: string;
  email?: string;
  role: Role = 'unknown';
  internSeasonName: string | null = null;
  mentorSeasonName: string | null = null;
  loading = true;

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
    } else {
      const decoded = this.decodeJwt(this.auth.accessToken);
      const roleFromJwt = this.extractRoleFromJwt(decoded);
      this.role = this.normalizeRole(roleFromJwt);
    }
    this.loading = false;
  }

  logout() {
    this.auth.logout();
    this.router.navigate(['/']);
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

  private decodeJwt(token: string | null): any | null {
    if (!token) return null;
    try { return JSON.parse(atob(token.split('.')[1] || '')); } catch { return null; }
  }

  private extractRoleFromJwt(payload: any): string | undefined {
    if (!payload) return undefined;
    const realmRoles: string[] | undefined = payload?.realm_access?.roles;
    if (realmRoles?.length) {
      const hit = realmRoles.find(r => ['admin','mentor','intern','guest'].includes(r.toLowerCase()));
      if (hit) return hit;
    }
    const res = payload?.resource_access;
    if (res && typeof res === 'object') {
      for (const k of Object.keys(res)) {
        const roles: string[] = res[k]?.roles || [];
        const hit = roles.find(r => ['admin','mentor','intern','guest'].includes(r.toLowerCase()));
        if (hit) return hit;
      }
    }
    return undefined;
  }
}
