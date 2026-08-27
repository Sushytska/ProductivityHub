import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { TaskRequest, TaskResponse } from '../models/task.model';

@Injectable({
  providedIn: 'root',
})
export class Tasks {
  private readonly http = inject(HttpClient);

  list(): Observable<TaskResponse[]> {
    return this.http.get<TaskResponse[]>('/api/Tasks');
  }

  get(id: string): Observable<TaskResponse> {
    return this.http.get<TaskResponse>(`/api/Tasks/${id}`);
  }

  create(task: TaskRequest): Observable<TaskResponse> {
    return this.http.post<TaskResponse>('/api/Tasks', task);
  }

  update(id: string, task: TaskRequest): Observable<TaskResponse> {
    return this.http.put<TaskResponse>(`/api/Tasks/${id}`, task);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`/api/Tasks/${id}`);
  }
}
