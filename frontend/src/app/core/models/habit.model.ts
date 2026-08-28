export interface HabitRequest {
  name: string;
  description: string | null;
}

export interface HabitResponse {
  id: string;
  name: string;
  description: string | null;
  createdDate: string;
  completedDates: string[];
  currentStreak: number;
  longestStreak: number;
}
