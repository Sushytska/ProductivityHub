import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Habits } from '../../../core/services/habits';
import { HabitResponse } from '../../../core/models/habit.model';

interface WeekCell {
  date: string;
  label: string;
  completed: boolean;
}

@Component({
  selector: 'app-habits-list',
  imports: [RouterLink],
  templateUrl: './habits-list.html',
  styleUrl: './habits-list.css',
})
export class HabitsList implements OnInit {
  private readonly habitsService = inject(Habits);

  protected readonly habits = signal<HabitResponse[]>([]);
  protected readonly loading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);
  private readonly togglingKeys = signal<ReadonlySet<string>>(new Set());

  ngOnInit(): void {
    this.habitsService.list(this.todayIso()).subscribe({
      next: (habits) => {
        this.habits.set(habits);
        this.loading.set(false);
      },
      error: () => {
        this.errorMessage.set('Could not load habits.');
        this.loading.set(false);
      },
    });
  }

  weekStrip(habit: HabitResponse): WeekCell[] {
    const completedSet = new Set(habit.completedDates);
    const cells: WeekCell[] = [];

    for (let i = 6; i >= 0; i--) {
      const date = new Date();
      date.setDate(date.getDate() - i);
      const iso = this.toIsoDate(date);
      cells.push({
        date: iso,
        label: date.toLocaleDateString(undefined, { weekday: 'short' }),
        completed: completedSet.has(iso),
      });
    }

    return cells;
  }

  isToggling(habit: HabitResponse, date: string): boolean {
    return this.togglingKeys().has(this.toggleKey(habit.id, date));
  }

  toggleDate(habit: HabitResponse, date: string): void {
    const key = this.toggleKey(habit.id, date);
    if (this.togglingKeys().has(key)) {
      return;
    }
    this.togglingKeys.update((keys) => new Set(keys).add(key));

    this.habitsService.toggle(habit.id, date, this.todayIso()).subscribe({
      next: (updated) => {
        this.habits.update((habits) => habits.map((h) => (h.id === habit.id ? updated : h)));
        this.clearToggling(key);
      },
      error: () => {
        this.errorMessage.set('Could not update the habit. Please try again.');
        this.clearToggling(key);
      },
    });
  }

  deleteHabit(id: string, event: Event): void {
    event.preventDefault();
    event.stopPropagation();

    if (!confirm('Delete this habit?')) {
      return;
    }

    this.habitsService.delete(id).subscribe({
      next: () => {
        this.habits.update((habits) => habits.filter((habit) => habit.id !== id));
      },
      error: () => {
        this.errorMessage.set('Could not delete the habit. Please try again.');
      },
    });
  }

  private toggleKey(habitId: string, date: string): string {
    return `${habitId}:${date}`;
  }

  private clearToggling(key: string): void {
    this.togglingKeys.update((keys) => {
      const next = new Set(keys);
      next.delete(key);
      return next;
    });
  }

  private todayIso(): string {
    return this.toIsoDate(new Date());
  }

  // Local calendar day, not date.toISOString() (which is UTC and can land on the
  // wrong day near midnight) — must match the DateOnly the week-strip button shows.
  private toIsoDate(date: Date): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }
}
