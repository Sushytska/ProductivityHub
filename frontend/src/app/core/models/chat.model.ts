export interface ChatSource {
  noteId: string;
  noteTitle: string;
  chunkIndex: number;
}

export interface ChatMetaEvent {
  sources: ChatSource[];
}

export interface ChatTokenEvent {
  text: string;
}

export interface ChatErrorEvent {
  message: string;
}

export type ChatMessageRole = 'user' | 'assistant';

export interface ChatMessage {
  role: ChatMessageRole;
  text: string;
  sources?: ChatSource[];
  isError?: boolean;
}
