# Content Calendar & Scheduled Publishing Engine

## Overview

A robust, multi-schedule calendar and scheduling engine supporting pure-calculation slot window pacing, recurrence via Cronos, a durable PostgreSQL task queue (`BackgroundTasks`), review outbox editorial triage, and a Tailwind-native calendar UI (Month, Week, Day views) with drag-and-drop queue management.

## Core Architectural Concepts

1. **Schedule Entities**:
   - Multiple schedules per user with dedicated IANA timezones (`Schedules` table).
   - Slot rules with recurrence (Daily, Weekly, Monthly, Yearly) and window capacities (`ScheduleSlotRules` table).
   - Equal-interval sub-slot pacing: $\Delta t = \frac{\text{EndTime} - \text{StartTime}}{\text{PostCapacity}}$ (e.g. 1 hour / 4 posts = sub-slots at :00, :15, :30, :45).

2. **3 Workflow Publishing Modes**:
   - **Manual**: Automated workflow generates drafts to the Review Outbox (`/outbox`) for editorial approval.
   - **Automatic**: Posts immediately upon generation.
   - **Scheduled**: Routes draft to the input queue of a specified schedule.

3. **Backend Execution**:
   - Engine: `SlotWindowCalculator` using `Cronos` (0.13.0) for timezone-aware recurrence evaluation.
   - Dispatcher: `ScheduleSlotEvaluatorService` scans active schedules, projects open sub-slots, matches queue items, and enqueues `"scheduled-publishing"` tasks in `BackgroundTasks`.
   - Worker: `ScheduledPublishingTaskHandler` publishes via `IPublisher` and records `Post` records.

4. **Frontend Experience**:
   - `/calendar`: Tailwind-native Month, Week, and Day views.
   - Schedule Manager modal: create/edit schedules, IANA timezone selector, slot rule recurrence editor with live pacing preview.
   - Input Queue sidebar: FIFO order, drag-and-drop / accessible reordering, pause/resume, deletion.
   - Draft Picker: queue existing drafts into any schedule.
   - Review Outbox (`/outbox`): editorial triage card feed to approve & queue or publish immediately.
   - Composer Split Button: `[ Publish Now | Add to Schedule ▾ ]`.
