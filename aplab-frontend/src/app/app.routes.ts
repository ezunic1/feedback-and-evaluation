import { Routes } from '@angular/router';
import { Landing } from './pages/landing/landing';
import { Login } from './pages/login/login';
import { Register } from './pages/register/register';
import { Profile } from './pages/profile/profile';
import { SeasonCreatePage } from './pages/season-create/season-create';

export const routes: Routes = [
  { path: '', component: Landing },
  { path: 'login', component: Login },
  { path: 'register', component: Register },
  { path: 'profile', component: Profile },

  { path: 'dashboard', loadComponent: () => import('./pages/dashboard/redirect/dashboard-redirect').then(m => m.DashboardRedirect) },
  { path: 'dashboard/admin', loadComponent: () => import('./pages/dashboard/admin-dashboard/admin-dashboard').then(m => m.AdminDashboard) },
  { path: 'dashboard/mentor', loadComponent: () => import('./pages/dashboard/mentor-dashboard/mentor-dashboard').then(m => m.MentorDashboard) },
  { path: 'dashboard/intern', loadComponent: () => import('./pages/dashboard/intern-dashboard/intern-dashboard').then(m => m.InternDashboard) },
  { path: 'users/new', loadComponent: () => import('./pages/user-create/user-create').then(m => m.UserCreatePage) },
  { path: 'seasons/new', component: SeasonCreatePage },
  { path: '', loadComponent: () => import('./pages/dashboard/admin-dashboard/admin-dashboard').then(m => m.AdminDashboard) },
  { path: 'seasons/:id', loadComponent: () => import('./pages/season/season').then(m => m.SeasonPage) },
  { path: '**', redirectTo: '' }
];
