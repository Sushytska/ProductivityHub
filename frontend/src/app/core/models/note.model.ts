export interface NoteRequest {
  title: string;
  content: string;
}

export interface NoteResponse {
  id: string;
  title: string;
  content: string;
  createdDate: string;
}
