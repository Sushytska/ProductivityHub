import { Component, DestroyRef, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ChatSocket } from '../../core/services/chat-socket';
import { ChatMessage } from '../../core/models/chat.model';

@Component({
  selector: 'app-chat',
  imports: [FormsModule],
  templateUrl: './chat.html',
  styleUrl: './chat.css',
})
export class Chat {
  protected readonly chatSocket = inject(ChatSocket);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly messages = signal<ChatMessage[]>([]);
  protected question = '';

  private previousBusy = false;

  constructor() {
    this.chatSocket.connect();
    this.destroyRef.onDestroy(() => this.chatSocket.disconnect());

    // busy flips true -> false exactly once per question, on either
    // chat:done or chat:error (see ChatSocket) — use that transition to
    // finalize the current streamed answer into the transcript.
    effect(() => {
      const busyNow = this.chatSocket.busy();

      if (this.previousBusy && !busyNow) {
        const error = this.chatSocket.currentError();
        if (error) {
          this.messages.update((msgs) => [...msgs, { role: 'assistant', text: error, isError: true }]);
        } else {
          this.messages.update((msgs) => [
            ...msgs,
            {
              role: 'assistant',
              text: this.chatSocket.currentAnswer(),
              sources: this.chatSocket.currentSources(),
            },
          ]);
        }
      }

      this.previousBusy = busyNow;
    });
  }

  submit(): void {
    const trimmed = this.question.trim();
    if (!trimmed || this.chatSocket.busy()) {
      return;
    }

    this.messages.update((msgs) => [...msgs, { role: 'user', text: trimmed }]);
    this.chatSocket.ask(trimmed);
    this.question = '';
  }
}
