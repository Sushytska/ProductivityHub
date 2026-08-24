import { Injectable, inject, signal } from '@angular/core';
import { Socket, io } from 'socket.io-client';
import { environment } from '../../../environments/environment';
import { Auth } from './auth';
import { ChatErrorEvent, ChatMetaEvent, ChatSource, ChatTokenEvent } from '../models/chat.model';

@Injectable({
  providedIn: 'root',
})
export class ChatSocket {
  private readonly auth = inject(Auth);
  private socket: Socket | null = null;

  readonly connected = signal(false);
  readonly connectionError = signal<string | null>(null);
  readonly busy = signal(false);
  readonly currentSources = signal<ChatSource[]>([]);
  readonly currentAnswer = signal('');
  readonly currentError = signal<string | null>(null);

  connect(): void {
    if (this.socket) {
      return;
    }

    // Same-origin in prod (empty environment.realtimeUrl, served through
    // Nginx alongside /api and /socket.io); direct :4000 connect in dev,
    // since realtime-service's CORS already defaults to "*".
    const url = environment.realtimeUrl || window.location.origin;
    const socket = io(url, { auth: { token: this.auth.getToken() } });
    this.socket = socket;

    socket.on('connect', () => {
      this.connected.set(true);
      this.connectionError.set(null);
    });

    socket.on('disconnect', () => {
      this.connected.set(false);
    });

    socket.on('connect_error', (err: Error) => {
      this.connected.set(false);
      const message =
        err.message === 'unauthorized' ? 'Your session has expired. Please log in again.' : err.message;
      this.connectionError.set(message);
      // Distinct from an in-band chat:error, but if a question was in flight
      // when the handshake/connection failed, the effect watching `busy` in
      // ChatComponent must still treat it as a failed answer, not a silent
      // success with an empty/partial currentAnswer — so mirror it here too.
      if (this.busy()) {
        this.currentError.set(message);
      }
      this.busy.set(false);
    });

    socket.on('chat:meta', (payload: ChatMetaEvent) => {
      this.currentSources.set(payload.sources);
    });

    socket.on('chat:token', (payload: ChatTokenEvent) => {
      this.currentAnswer.update((text) => text + payload.text);
    });

    socket.on('chat:done', () => {
      this.busy.set(false);
    });

    socket.on('chat:error', (payload: ChatErrorEvent) => {
      this.currentError.set(payload.message);
      this.busy.set(false);
    });
  }

  ask(question: string): void {
    if (!this.socket || this.busy()) {
      return;
    }

    this.currentAnswer.set('');
    this.currentSources.set([]);
    this.currentError.set(null);
    this.busy.set(true);
    this.socket.emit('chat:ask', { question });
  }

  disconnect(): void {
    this.socket?.disconnect();
    this.socket = null;
    this.connected.set(false);
    // ChatSocket is a root-singleton, so its state outlives ChatComponent —
    // without this, navigating away mid-stream (busy=true, socket torn down
    // before chat:done/chat:error could ever arrive) would leave `busy` stuck
    // true forever, permanently disabling ask() on every future /chat visit.
    this.busy.set(false);
  }
}
