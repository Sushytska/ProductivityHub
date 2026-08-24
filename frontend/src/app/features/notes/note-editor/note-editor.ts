import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Notes } from '../../../core/services/notes';
import { NormalizedError } from '../../../core/interceptors/error-interceptor';

@Component({
  selector: 'app-note-editor',
  imports: [FormsModule, RouterLink],
  templateUrl: './note-editor.html',
  styleUrl: './note-editor.css',
})
export class NoteEditor implements OnInit {
  private readonly notesService = inject(Notes);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected noteId: string | null = null;
  protected title = '';
  protected content = '';
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

    this.noteId = idParam;
    this.notesService.get(idParam).subscribe({
      next: (note) => {
        this.title = note.title;
        this.content = note.content;
        this.loading.set(false);
      },
      // Notes/{id} returns an empty-body 404 for missing/foreign notes —
      // show an inline "not found" state instead of crashing on an empty body.
      error: () => {
        this.notFound.set(true);
        this.loading.set(false);
      },
    });
  }

  save(): void {
    if (!this.title.trim() || !this.content.trim() || this.saving()) {
      return;
    }

    this.saving.set(true);
    this.errorMessage.set(null);

    const request = { title: this.title, content: this.content };
    const save$ = this.noteId
      ? this.notesService.update(this.noteId, request)
      : this.notesService.create(request);

    save$.subscribe({
      next: () => this.router.navigateByUrl('/notes'),
      error: (error: NormalizedError) => {
        this.saving.set(false);
        this.errorMessage.set(error.message);
      },
    });
  }

  delete(): void {
    if (!this.noteId || !confirm('Delete this note?')) {
      return;
    }

    this.notesService.delete(this.noteId).subscribe({
      next: () => this.router.navigateByUrl('/notes'),
      error: (error: NormalizedError) => this.errorMessage.set(error.message),
    });
  }
}
