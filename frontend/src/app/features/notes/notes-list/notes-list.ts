import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Notes } from '../../../core/services/notes';
import { NoteResponse } from '../../../core/models/note.model';

@Component({
  selector: 'app-notes-list',
  imports: [RouterLink, DatePipe],
  templateUrl: './notes-list.html',
  styleUrl: './notes-list.css',
})
export class NotesList implements OnInit {
  private readonly notesService = inject(Notes);

  protected readonly notes = signal<NoteResponse[]>([]);
  protected readonly loading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);

  ngOnInit(): void {
    this.notesService.list().subscribe({
      next: (notes) => {
        this.notes.set(notes);
        this.loading.set(false);
      },
      error: () => {
        this.errorMessage.set('Could not load notes.');
        this.loading.set(false);
      },
    });
  }

  deleteNote(id: string, event: Event): void {
    event.preventDefault();
    event.stopPropagation();

    if (!confirm('Delete this note?')) {
      return;
    }

    this.notesService.delete(id).subscribe({
      next: () => {
        this.notes.update((notes) => notes.filter((note) => note.id !== id));
      },
      error: () => {
        this.errorMessage.set('Could not delete the note. Please try again.');
      },
    });
  }
}
