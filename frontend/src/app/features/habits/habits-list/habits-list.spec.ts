import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { HabitsList } from './habits-list';
import { HabitResponse } from '../../../core/models/habit.model';

function toIsoDate(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

function habit(overrides: Partial<HabitResponse> = {}): HabitResponse {
  return {
    id: '1',
    name: 'Test habit',
    description: null,
    createdDate: '2026-01-01T00:00:00Z',
    completedDates: [],
    currentStreak: 0,
    longestStreak: 0,
    ...overrides,
  };
}

describe('HabitsList', () => {
  let component: HabitsList;
  let fixture: ComponentFixture<HabitsList>;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HabitsList],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(HabitsList);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('weekStrip returns 7 cells ending today, in local calendar days', () => {
    const todayIso = toIsoDate(new Date());
    const cells = component.weekStrip(habit({ completedDates: [todayIso] }));

    expect(cells.length).toBe(7);
    expect(cells[6].date).toBe(todayIso);
    expect(cells[6].completed).toBe(true);
    expect(cells[0].completed).toBe(false);
  });

  it('does not send a second toggle request for the same cell while one is in flight', () => {
    httpMock.expectOne((req) => req.url === '/api/Habits').flush([]);
    const h = habit();

    component.toggleDate(h, '2026-01-01');
    component.toggleDate(h, '2026-01-01');

    httpMock.expectOne('/api/Habits/1/toggle');
    httpMock.verify();
  });

  it('allows toggling a different cell while another is in flight', () => {
    httpMock.expectOne((req) => req.url === '/api/Habits').flush([]);
    const h = habit();

    component.toggleDate(h, '2026-01-01');
    component.toggleDate(h, '2026-01-02');

    httpMock.expectOne(
      (req) => req.url === '/api/Habits/1/toggle' && req.body.date === '2026-01-01',
    );
    httpMock.expectOne(
      (req) => req.url === '/api/Habits/1/toggle' && req.body.date === '2026-01-02',
    );
    httpMock.verify();
  });
});
