import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth-guard';

export const routes: Routes = [
  { path: '', redirectTo: 'notes', pathMatch: 'full' },
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login/login').then((m) => m.Login),
  },
  {
    path: 'register',
    loadComponent: () => import('./features/auth/register/register').then((m) => m.Register),
  },
  // notes/new must come before notes/:id — route order matters, a param
  // route declared first would otherwise swallow the literal "new" segment.
  {
    path: 'notes/new',
    loadComponent: () =>
      import('./features/notes/note-editor/note-editor').then((m) => m.NoteEditor),
    canActivate: [authGuard],
  },
  {
    path: 'notes/:id',
    loadComponent: () =>
      import('./features/notes/note-editor/note-editor').then((m) => m.NoteEditor),
    canActivate: [authGuard],
  },
  {
    path: 'notes',
    loadComponent: () =>
      import('./features/notes/notes-list/notes-list').then((m) => m.NotesList),
    canActivate: [authGuard],
  },
  {
    path: 'chat',
    loadComponent: () => import('./features/chat/chat').then((m) => m.Chat),
    canActivate: [authGuard],
  },
  { path: '**', redirectTo: 'notes' },
];
