import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';

import { NoteEditor } from './note-editor';

describe('NoteEditor', () => {
  let component: NoteEditor;
  let fixture: ComponentFixture<NoteEditor>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [NoteEditor],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(NoteEditor);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
