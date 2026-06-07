# Personal Dashboard

A personal productivity and learning dashboard built with ASP.NET Core Blazor. It centralizes technical articles, quiz questions with spaced-repetition review, job market data, and interactive coding challenges — all behind a role-based auth system.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core 9 · Blazor Server (Interactive) |
| Database | SQL Server · Entity Framework Core 9 |
| Auth | ASP.NET Core Identity (cookie-based, roles) |
| Frontend | Bootstrap 5 · Summernote rich-text editor |
| Content safety | HtmlSanitizer |
| Deployment | Docker (multi-stage Linux build) |

---

## Features

### Articles
Full CRUD for technical articles. Each article supports a rich-text body (Summernote), cover image, labels, and an `IsPublic` visibility flag. Articles can be linked to quiz questions as reference material.

### Technical Quiz
Multiple-choice questions (4 choices, single correct answer) with explanations. Questions are grouped by linked article (theme) or fall into a General bucket. Admins can create, edit, and delete questions from the UI.

### Leitner Cards — Spaced Repetition
A 20-box Leitner system that schedules quiz questions for review. Correct answers move a card forward; wrong answers reset it. A "Today's Questions" view surfaces only what is due, making daily review fast.

### Job Market Data
Aggregated job posting data with charts and filters by role, city, and site. Useful for tracking market trends over time.

### Coding Challenges
Interactive in-browser coding challenges. Currently features a Singleton pattern challenge: the submitted C# code is compiled and tested server-side in real time.

