export interface TaskRequest {
  title: string;
  description: string | null;
  isCompleted: boolean;
  dueDate: string | null;
}

export interface TaskResponse {
  id: string;
  title: string;
  description: string | null;
  isCompleted: boolean;
  dueDate: string | null;
  createdDate: string;
}
