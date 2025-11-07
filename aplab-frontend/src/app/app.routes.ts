import { Routes } from '@angular/router';
import { Landing } from './pages/landing/landing';
import { Login } from './pages/login/login';
import { Register } from './pages/register/register';
import { Profile } from './pages/profile/profile';

export const routes: Routes = [
  { path: '', component: Landing },
  { path: 'login', component: Login },
  { path: 'register', component: Register },
  { path: 'profile', component: Profile },
  {path: 'dashboard', loadComponent: () => import('./pages/dashboard/dashboard').then(m => m.Dashboard)},
  {path: 'seasons/:id', loadComponent: () => import('./pages/season/season').then(m => m.SeasonPage)},
  { path: '**', redirectTo: '' }
];
