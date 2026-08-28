# 🧠 ProductivityHub

**Self-hosted productivity workspace with an AI assistant that knows your notes**

Notes · Tasks · Habit Tracker · RAG-powered AI Chat

---

## What is this?

ProductivityHub is a **self-hosted personal productivity app** you run on your own machine or homelab. It combines notes, tasks and habit tracking in one place — and adds an AI assistant that answers questions about **your own content**, not just generic knowledge.

> Ask *"What did I write about Docker networking last week?"* and the AI finds and uses the exact relevant parts of your notes to answer.

This is an open-source portfolio project built to demonstrate a full-stack RAG (Retrieval-Augmented Generation) implementation with a modern .NET backend.

---

## How the AI chat works

Most AI assistants are trained on public data. This one reads **your notes**.

```
You ask a question
        ↓
Your question is converted to a vector (embedding)
        ↓
pgvector searches your notes for the most relevant chunks
        ↓
Top 3–10 chunks are passed to the AI as context
        ↓
AI answers based on what you actually wrote
```

Long notes are split into ~500-word chunks, each with its own embedding vector. This means the AI can pinpoint a single relevant paragraph rather than loading entire documents into the context window.

---

## Features

- 📝 **Notes** — create, edit and search your knowledge base
- ✅ **Tasks** — manage your to-dos
- 📊 **Habit Tracker** — track recurring activities
- 🤖 **AI Chat with RAG** — ask questions, get answers grounded in your own notes
- ⚡ **Streaming responses** — AI replies appear word by word, relayed in real time via a Node.js + Socket.IO service
- 🔒 **Self-hosted** — your data never leaves your server
- 🐳 **Docker-first** — one command to run everything

---

## Status

This project is under active development. What's actually built so far vs. what's on the roadmap:

**Implemented**
- ✅ JWT authentication (register / login)
- ✅ Notes CRUD, scoped to the authenticated user
- ✅ Background note chunking + embedding generation (Ollama `nomic-embed-text`, Redis-backed queue, retry with backoff)
- ✅ RAG-powered AI chat (`POST /api/chat` — pgvector similarity search over your notes *and* tasks, ranked and merged together, + Anthropic Claude for grounded answers — e.g. "when is my electricity bill due?" is answered from a Task, "what did I write about Docker?" from a Note)
- ✅ Streaming chat responses (`POST /api/chat/stream`, Server-Sent Events)
- ✅ Realtime layer (Node.js + Socket.IO relay in `realtime-service/`, consuming the SSE endpoint)
- ✅ Frontend (Angular SPA in `frontend/`) — login/register, notes list + editor, and a chat interface streaming answers via `realtime-service` over Socket.IO
- ✅ Full-stack Docker Compose (`API-server/docker-compose.yml`) — Postgres, Redis, the API, `realtime-service`, and Nginx (serving the Angular build and reverse-proxying `/api` and `/socket.io`), plus an opt-in containerized Ollama
- ✅ Tasks CRUD (`api/Tasks`), scoped to the authenticated user, with a due date, a completed flag, and a matching Angular list/editor UI (inline complete-toggle, incomplete-first sorting), plus its own embedding pipeline (title + status + due date + description) feeding the same RAG chat as notes
- ✅ HNSW indexes (`vector_cosine_ops`) on both `NoteChunks.Embedding` and `Tasks.Embedding` — similarity search no longer falls back to a sequential scan
- ✅ Habit Tracker (`api/Habits`), scoped to the authenticated user — daily habits with a per-day completion toggle (`api/Habits/{id}/toggle`), current/longest streak calculation, and a matching Angular list/editor UI (7-day week-strip toggle). Not wired into RAG chat — a habit's content (a name + a completion calendar) is a poor fit for semantic search.

**Planned**
- (none — all currently planned features are implemented)

---

## Known limitations

The embedding pipeline is a personal-project MVP, not a production-hardened job queue. Known gaps, accepted for now:

- **Migration isn't safe on databases with existing embeddings.** The `vector(1536)` → `vector(768)` column change in `AddEmbeddingPipelineColumns` has no data-clearing step; pgvector rejects the in-place resize if any `NoteChunk` rows already have a 1536-dim vector. Harmless on a fresh database (this repo's own migration history), but re-running the embedding pipeline's schema migrations against an environment that already embedded notes under the old dimension will fail and needs manual intervention (clear the `NoteChunks` table first).
- **Race condition on concurrent edit + processing.** `Note` has no optimistic-concurrency token. If a note is edited while the background worker is still embedding an earlier version of it, the worker's completion write can overwrite the user's fresh `Pending` status with `Completed` — leaving the note briefly marked done with embeddings from the *previous* content. Self-heals on the next processing pass (the edit's own re-enqueue still runs), since only one worker instance exists today; would need a concurrency token if the worker were ever scaled out.
- **Redundant Ollama calls on rapid edits.** `NoteService.UpdateAsync` re-enqueues on every save with no dedup — editing the same note several times in quick succession queues one embedding job per save, each fully re-chunking and re-embedding. Wasted work, not incorrect behavior; not worth a dedup layer for single-user local use.
- **No relevance cutoff on RAG retrieval.** `RagService` always returns up to its top-K notes/tasks combined if the user has *any* embedded content, even for an unrelated question — there's no cosine-distance threshold. The model is prompted to say honestly when the retrieved context doesn't answer the question, which works in practice, but an untuned distance cutoff was deliberately deferred rather than guessed at.
- **Streaming has no reconnect/replay.** If the Node.js relay or the browser disconnects mid-stream, the partial answer already generated is simply lost — no persistence happens for an interrupted stream, and there's no mechanism to resume or replay a dropped SSE connection. Acceptable for a single-user local tool; would need real infrastructure (a job/event log) to fix properly.
- **Notes have no embedding-status indicator in the UI.** `NoteResponse` doesn't expose `Note.EmbeddingStatus`, so the frontend can't yet show whether a note's embeddings are still processing — a backend DTO change would be needed first.

---

## Tech stack

| Layer | Technology |
|---|---|
| **Backend API** | ASP.NET Core (.NET 9) · Minimal API |
| **Realtime** | Node.js |
| **Frontend** | Angular (SPA) |
| **Database** | PostgreSQL · pgvector |
| **ORM** | Entity Framework Core · Code First |
| **AI (cloud)** | Anthropic |
| **Infrastructure** | Docker Compose |

---
