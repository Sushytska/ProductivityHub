import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { HabitRequest, HabitResponse } from '../models/habit.model';

@Injectable({
  providedIn: 'root',
})
export class Habits {
  private readonly http = inject(HttpClient);

  // asOfDate is the caller's local calendar day, so CurrentStreak agrees with whatever
  // "today" the UI itself is showing (e.g. the week-strip) rather than server UTC.
  list(asOfDate: string): Observable<HabitResponse[]> {
    return this.http.get<HabitResponse[]>('/api/Habits', { params: { asOfDate } });
  }

  get(id: string): Observable<HabitResponse> {
    return this.http.get<HabitResponse>(`/api/Habits/${id}`);
  }

  create(habit: HabitRequest): Observable<HabitResponse> {
    return this.http.post<HabitResponse>('/api/Habits', habit);
  }

  update(id: string, habit: HabitRequest): Observable<HabitResponse> {
    return this.http.put<HabitResponse>(`/api/Habits/${id}`, habit);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`/api/Habits/${id}`);
  }

  toggle(id: string, date: string, asOfDate: string): Observable<HabitResponse> {
    return this.http.post<HabitResponse>(`/api/Habits/${id}/toggle`, { date, asOfDate });
  }
}
