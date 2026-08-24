import { inject } from '@angular/core';
import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';
import { Auth } from '../services/auth';

// Backend error shapes are inconsistent across controllers: plain JSON-encoded
// strings (Auth 400/401), empty bodies (Notes 404/204), ProblemDetails/
// ValidationProblemDetails (Chat 502, framework validation), and unstyled 500s.
// This normalizes all of them into one shape components can rely on.
export interface NormalizedError {
  status: number;
  message: string;
}

function extractMessage(error: HttpErrorResponse): string {
  const body: unknown = error.error;

  if (typeof body === 'string' && body.trim().length > 0) {
    return body;
  }

  if (body && typeof body === 'object') {
    const problem = body as { detail?: string; title?: string; errors?: Record<string, string[]> };

    if (problem.detail) {
      return problem.detail;
    }
    if (problem.errors) {
      const firstField = Object.values(problem.errors)[0];
      if (Array.isArray(firstField) && firstField.length > 0) {
        return firstField[0];
      }
    }
    if (problem.title) {
      return problem.title;
    }
  }

  if (error.status === 0) {
    return 'Could not reach the server. Check your connection and try again.';
  }
  if (error.status === 404) {
    return 'Not found.';
  }
  if (error.status === 429) {
    return 'Too many requests — please slow down and try again shortly.';
  }

  return 'An unexpected error occurred. Please try again.';
}

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(Auth);

  return next(req).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse)) {
        return throwError(() => error);
      }

      // Only treat 401 as "session expired" when we actually thought we were
      // logged in — otherwise a plain wrong-password login attempt would
      // trigger a pointless logout()/redirect while already on /login.
      if (error.status === 401 && req.url.startsWith('/api/') && auth.getToken() !== null) {
        auth.logout();
      }

      const normalized: NormalizedError = {
        status: error.status,
        message: extractMessage(error),
      };
      return throwError(() => normalized);
    }),
  );
};
