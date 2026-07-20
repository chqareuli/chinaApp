import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { entryCreatorGuard } from './core/guards/entry-creator.guard';
import { guestGuard } from './core/guards/guest.guard';

export const routes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/login-page/login-page').then((m) => m.LoginPage),
    title: 'შესვლა — სარედაქციო პანელი',
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/app-shell/app-shell').then((m) => m.AppShell),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        loadComponent: () =>
          import('./features/dashboard/dashboard-page/dashboard-page').then((m) => m.DashboardPage),
        title: 'მთავარი — სარედაქციო პანელი',
      },
      {
        path: 'entries/new',
        canActivate: [entryCreatorGuard],
        loadComponent: () =>
          import('./features/entries/create-entry-page/create-entry-page').then((m) => m.CreateEntryPage),
        title: 'ახალი სტატია — სარედაქციო პანელი',
      },
      {
        path: 'entries/:id',
        loadComponent: () =>
          import('./features/entries/entry-detail-page/entry-detail-page').then((m) => m.EntryDetailPage),
        title: 'სტატია — სარედაქციო პანელი',
      },
      {
        path: 'notifications',
        data: {
          title: 'ნოთიფიკაციები',
          description: 'სტატიების ცვლილებების შეტყობინებების სამუშაო სივრცე შემდეგ ინკრემენტში დაემატება.',
        },
        loadComponent: () =>
          import('./shared/coming-soon-page/coming-soon-page').then((m) => m.ComingSoonPage),
        title: 'ნოთიფიკაციები — სარედაქციო პანელი',
      },
      {
        path: 'messages',
        data: {
          title: 'პირადი წერილები',
          description: 'საუბრის მიხედვით დაჯგუფებული მიმოწერის UI შემდეგ ინკრემენტში დაემატება.',
        },
        loadComponent: () =>
          import('./shared/coming-soon-page/coming-soon-page').then((m) => m.ComingSoonPage),
        title: 'წერილები — სარედაქციო პანელი',
      },
      {
        path: 'accounting',
        data: {
          title: 'აღრიცხვა',
          description: 'რედაქტორების ქულებისა და სტატისტიკის სრული გვერდი შემდეგ ინკრემენტში დაემატება.',
        },
        loadComponent: () =>
          import('./shared/coming-soon-page/coming-soon-page').then((m) => m.ComingSoonPage),
        title: 'აღრიცხვა — სარედაქციო პანელი',
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
