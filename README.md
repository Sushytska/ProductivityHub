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
- ⚡ **Streaming responses** — AI replies appear word by word via WebSockets
- 🔒 **Self-hosted** — your data never leaves your server
- 🐳 **Docker-first** — one command to run everything

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
