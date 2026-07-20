import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.isAuthenticated()) {
    return true;
  }

  const loginTree = () =>
    router.createUrlTree(['/adminpanel'], { queryParams: { returnUrl: state.url } });
  if (!auth.hasSession()) {
    return loginTree();
  }

  return auth.refresh().pipe(
    map(() => true),
    catchError(() => of(loginTree())),
  );
};
