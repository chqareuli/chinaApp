import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const entryCreatorGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const role = auth.editor()?.role;
  return role === 'SuperAdmin' ||
    role === 'ChiefEditor' ||
    role === 'DeputyChiefEditor' ||
    role === 'Editor' ||
    role === 'AssistantEditor'
    ? true
    : router.createUrlTree(['/adminpanel/dashboard']);
};
