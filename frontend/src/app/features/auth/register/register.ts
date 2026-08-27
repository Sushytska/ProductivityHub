import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Auth } from '../../../core/services/auth';
import { NormalizedError } from '../../../core/interceptors/error-interceptor';

@Component({
  selector: 'app-register',
  imports: [FormsModule, RouterLink],
  templateUrl: './register.html',
  styleUrl: './register.css',
})
export class Register {
  private readonly auth = inject(Auth);
  private readonly router = inject(Router);

  protected email = '';
  protected password = '';
  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  submit(): void {
    if (!this.email || !this.password || this.submitting()) {
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set(null);

    // The backend returns a token on register too, so this is effectively
    // an auto-login — no separate "please log in" step needed.
    this.auth.register(this.email, this.password).subscribe({
      next: () => this.router.navigateByUrl('/notes'),
      error: (error: NormalizedError) => {
        this.submitting.set(false);
        this.errorMessage.set(error.message);
      },
    });
  }
}
