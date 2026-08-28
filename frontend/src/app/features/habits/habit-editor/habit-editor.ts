import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Habits } from '../../../core/services/habits';
import { NormalizedError } from '../../../core/interceptors/error-interceptor';

@Component({
  selector: 'app-habit-editor',
  imports: [FormsModule, RouterLink],
  templateUrl: './habit-editor.html',
  styleUrl: './habit-editor.css',
})
export class HabitEditor implements OnInit {
  private readonly habitsService = inject(Habits);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected habitId: string | null = null;
  protected name = '';
  protected description = '';
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly notFound = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');

    if (!idParam || idParam === 'new') {
      this.loading.set(false);
      return;
    }

    this.habitId = idParam;
    this.habitsService.get(idParam).subscribe({
      next: (habit) => {
        this.name = habit.name;
        this.description = habit.description ?? '';
        this.loading.set(false);
      },
      // Habits/{id} returns an empty-body 404 for missing/foreign habits —
      // show an inline "not found" state instead of crashing on an empty body.
      error: () => {
        this.notFound.set(true);
        this.loading.set(false);
      },
    });
  }

  save(): void {
    if (!this.name.trim() || this.saving()) {
      return;
    }

    this.saving.set(true);
    this.errorMessage.set(null);

    const request = {
      name: this.name,
      description: this.description.trim() ? this.description : null,
    };
    const save$ = this.habitId
      ? this.habitsService.update(this.habitId, request)
      : this.habitsService.create(request);

    save$.subscribe({
      next: () => this.router.navigateByUrl('/habits'),
      error: (error: NormalizedError) => {
        this.saving.set(false);
        this.errorMessage.set(error.message);
      },
    });
  }

  delete(): void {
    if (!this.habitId || !confirm('Delete this habit?')) {
      return;
    }

    this.habitsService.delete(this.habitId).subscribe({
      next: () => this.router.navigateByUrl('/habits'),
      error: (error: NormalizedError) => this.errorMessage.set(error.message),
    });
  }
}
