import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { NoteRequest, NoteResponse } from '../models/note.model';

@Injectable({
  providedIn: 'root',
})
export class Notes {
  private readonly http = inject(HttpClient);

  list(): Observable<NoteResponse[]> {
    return this.http.get<NoteResponse[]>('/api/Notes');
  }

  get(id: string): Observable<NoteResponse> {
    return this.http.get<NoteResponse>(`/api/Notes/${id}`);
  }

  create(note: NoteRequest): Observable<NoteResponse> {
    return this.http.post<NoteResponse>('/api/Notes', note);
  }

  update(id: string, note: NoteRequest): Observable<NoteResponse> {
    return this.http.put<NoteResponse>(`/api/Notes/${id}`, note);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`/api/Notes/${id}`);
  }
}
