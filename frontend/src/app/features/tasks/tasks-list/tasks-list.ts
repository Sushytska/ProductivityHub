import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Tasks } from '../../../core/services/tasks';
import { TaskResponse } from '../../../core/models/task.model';

@Component({
  selector: 'app-tasks-list',
  imports: [RouterLink],
  templateUrl: './tasks-list.html',
  styleUrl: './tasks-list.css',
})
export class TasksList implements OnInit {
  private readonly tasksService = inject(Tasks);

  protected readonly tasks = signal<TaskResponse[]>([]);
  protected readonly loading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);

  ngOnInit(): void {
    this.tasksService.list().subscribe({
      next: (tasks) => {
        this.tasks.set(tasks);
        this.loading.set(false);
      },
      error: () => {
        this.errorMessage.set('Could not load tasks.');
        this.loading.set(false);
      },
    });
  }

  toggleCompleted(task: TaskResponse): void {
    const updated: TaskResponse = { ...task, isCompleted: !task.isCompleted };
    this.tasks.update((tasks) => tasks.map((t) => (t.id === task.id ? updated : t)));

    this.tasksService
      .update(task.id, {
        title: task.title,
        description: task.description,
        isCompleted: !task.isCompleted,
        dueDate: task.dueDate,
      })
      .subscribe({
        error: () => {
          // Revert the optimistic update on failure.
          this.tasks.update((tasks) => tasks.map((t) => (t.id === task.id ? task : t)));
          this.errorMessage.set('Could not update the task. Please try again.');
        },
      });
  }

  deleteTask(id: string, event: Event): void {
    event.preventDefault();
    event.stopPropagation();

    if (!confirm('Delete this task?')) {
      return;
    }

    this.tasksService.delete(id).subscribe({
      next: () => {
        this.tasks.update((tasks) => tasks.filter((task) => task.id !== id));
      },
      error: () => {
        this.errorMessage.set('Could not delete the task. Please try again.');
      },
    });
  }
}
