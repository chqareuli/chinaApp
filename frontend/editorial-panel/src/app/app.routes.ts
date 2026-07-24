import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { entryCreatorGuard } from './core/guards/entry-creator.guard';
import { guestGuard } from './core/guards/guest.guard';
import { superAdminGuard } from './core/guards/super-admin.guard';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () =>
      import('./features/public-dictionary/public-dictionary-page/public-dictionary-page').then(
        (m) => m.PublicDictionaryPage,
      ),
    title: 'ჩინურ-ქართული ლექსიკონი — Chinese.ge',
  },
  {
    path: 'word/:id',
    loadComponent: () =>
      import('./features/public-dictionary/public-dictionary-entry-page/public-dictionary-entry-page').then(
        (m) => m.PublicDictionaryEntryPage,
      ),
    title: 'სიტყვა — ჩინურ-ქართული ლექსიკონი',
  },
  {
    path: 'adminpanel',
    pathMatch: 'full',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/login-page/login-page').then((m) => m.LoginPage),
    title: 'შესვლა — სარედაქციო პანელი',
  },
  {
    path: 'adminpanel',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/app-shell/app-shell').then((m) => m.AppShell),
    children: [
      {
        path: 'dashboard',
        loadComponent: () =>
          import('./features/dashboard/dashboard-page/dashboard-page').then((m) => m.DashboardPage),
        title: 'მთავარი — სარედაქციო პანელი',
      },
      {
        path: 'entries',
        pathMatch: 'full',
        loadComponent: () =>
          import('./features/entries/entries-list-page/entries-list-page').then(
            (m) => m.EntriesListPage,
          ),
        title: 'სტატიები — სარედაქციო პანელი',
      },
      {
        path: 'entries/new',
        canActivate: [entryCreatorGuard],
        loadComponent: () =>
          import('./features/entries/create-entry-page/create-entry-page').then(
            (m) => m.CreateEntryPage,
          ),
        title: 'ახალი სტატია — სარედაქციო პანელი',
      },
      {
        path: 'entries/:id',
        loadComponent: () =>
          import('./features/entries/entry-detail-page/entry-detail-page').then(
            (m) => m.EntryDetailPage,
          ),
        title: 'სტატია — სარედაქციო პანელი',
      },
      {
        path: 'notifications',
        loadComponent: () =>
          import('./features/notifications/notifications-page/notifications-page').then(
            (m) => m.NotificationsPage,
          ),
        title: 'ნოტიფიკაციები — სარედაქციო პანელი',
      },
      {
        path: 'messages',
        loadComponent: () =>
          import('./features/messages/messages-page/messages-page').then((m) => m.MessagesPage),
        title: 'წერილები — სარედაქციო პანელი',
      },
      {
        path: 'accounting',
        loadComponent: () =>
          import('./features/accounting/accounting-page/accounting-page').then(
            (m) => m.AccountingPage,
          ),
        title: 'აღრიცხვა — სარედაქციო პანელი',
      },
      {
        path: 'profile',
        loadComponent: () =>
          import('./features/profile/profile-page/profile-page').then((m) => m.ProfilePage),
        title: 'ჩემი პროფილი — სარედაქციო პანელი',
      },
      {
        path: 'users',
        canActivate: [superAdminGuard],
        loadComponent: () =>
          import('./features/users/users-page/users-page').then((m) => m.UsersPage),
        title: 'მომხმარებლები — სარედაქციო პანელი',
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
