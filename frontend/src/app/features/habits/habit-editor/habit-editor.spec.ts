import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';

import { HabitEditor } from './habit-editor';

describe('HabitEditor', () => {
  let component: HabitEditor;
  let fixture: ComponentFixture<HabitEditor>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HabitEditor],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(HabitEditor);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
