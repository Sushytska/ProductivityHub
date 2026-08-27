import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Tasks } from '../../../core/services/tasks';
import { NormalizedError } from '../../../core/interceptors/error-interceptor';

@Component({
  selector: 'app-task-editor',
  imports: [FormsModule, RouterLink],
  templateUrl: './task-editor.html',
  styleUrl: './task-editor.css',
})
export class TaskEditor implements OnInit {
  private readonly tasksService = inject(Tasks);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected taskId: string | null = null;
  protected title = '';
  protected description = '';
  protected dueDate = '';
  protected isCompleted = false;
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

    this.taskId = idParam;
    this.tasksService.get(idParam).subscribe({
      next: (task) => {
        this.title = task.title;
        this.description = task.description ?? '';
        this.dueDate = task.dueDate ?? '';
        this.isCompleted = task.isCompleted;
        this.loading.set(false);
      },
      // Tasks/{id} returns an empty-body 404 for missing/foreign tasks —
      // show an inline "not found" state instead of crashing on an empty body.
      error: () => {
        this.notFound.set(true);
        this.loading.set(false);
      },
    });
  }

  save(): void {
    if (!this.title.trim() || this.saving()) {
      return;
    }

    this.saving.set(true);
    this.errorMessage.set(null);

    const request = {
      title: this.title,
      description: this.description.trim() ? this.description : null,
      isCompleted: this.isCompleted,
      dueDate: this.dueDate ? this.dueDate : null,
    };
    const save$ = this.taskId
      ? this.tasksService.update(this.taskId, request)
      : this.tasksService.create(request);

    save$.subscribe({
      next: () => this.router.navigateByUrl('/tasks'),
      error: (error: NormalizedError) => {
        this.saving.set(false);
        this.errorMessage.set(error.message);
      },
    });
  }

  delete(): void {
    if (!this.taskId || !confirm('Delete this task?')) {
      return;
    }

    this.tasksService.delete(this.taskId).subscribe({
      next: () => this.router.navigateByUrl('/tasks'),
      error: (error: NormalizedError) => this.errorMessage.set(error.message),
    });
  }
}
