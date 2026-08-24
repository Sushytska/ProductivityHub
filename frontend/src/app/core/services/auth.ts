import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { AuthResponse, LoginRequest, MeResponse, RegisterRequest } from '../models/auth.model';

const TOKEN_KEY = 'ph_token';

@Injectable({
  providedIn: 'root',
})
export class Auth {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly tokenSignal = signal<string | null>(localStorage.getItem(TOKEN_KEY));
  readonly isAuthenticated = computed(() => this.tokenSignal() !== null);

  login(email: string, password: string): Observable<AuthResponse> {
    const body: LoginRequest = { email, password };
    return this.http
      .post<AuthResponse>('/api/Auth/login', body)
      .pipe(tap((response) => this.storeToken(response.token)));
  }

  register(email: string, password: string): Observable<AuthResponse> {
    const body: RegisterRequest = { email, password };
    return this.http
      .post<AuthResponse>('/api/Auth/register', body)
      .pipe(tap((response) => this.storeToken(response.token)));
  }

  fetchMe(): Observable<MeResponse> {
    return this.http.get<MeResponse>('/api/Auth/me');
  }

  logout(): void {
    localStorage.removeItem(TOKEN_KEY);
    this.tokenSignal.set(null);
    this.router.navigateByUrl('/login');
  }

  getToken(): string | null {
    return this.tokenSignal();
  }

  private storeToken(token: string): void {
    localStorage.setItem(TOKEN_KEY, token);
    this.tokenSignal.set(token);
  }
}
