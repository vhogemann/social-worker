# Scheduled Publishing & Content Calendar Engine

This folder contains the complete technical specification and phased implementation plans for the Content Calendar & Scheduled Publishing Engine.

## Master Documents

* **Architecture Specification**: [planning/SCHEDULED_PUBLISHING.md](../SCHEDULED_PUBLISHING.md)
* **Backend Implementation Plan**: [planning/scheduling/BACKEND_IMPLEMENTATION_PLAN.md](BACKEND_IMPLEMENTATION_PLAN.md)
* **Frontend Implementation Plan**: [planning/scheduling/FRONTEND_IMPLEMENTATION_PLAN.md](FRONTEND_IMPLEMENTATION_PLAN.md)

## Phased Milestones

### Backend (B1 – B4)
1. **Milestone B1**: Domain Entities (`Schedule`, `ScheduleSlotRule`, `ScheduleQueueItem`), DbUp Migration `0012_add_schedules_and_calendar.sql`, EF Core Mappings, and `Cronos` (`0.13.0`) onboarding.
2. **Milestone B2**: Recurrence Engine (`CronExpressionBuilder`) & Slot Window Pacing Service (`SlotWindowCalculator`).
3. **Milestone B3**: Queue Manager (`ScheduleQueueService`), Dispatcher (`ScheduleSlotEvaluatorService`), and Background Task Handler (`ScheduledPublishingTaskHandler` on PostgreSQL 16 `SKIP LOCKED`).
4. **Milestone B4**: REST Endpoints (`/api/schedules`, `/api/outbox`, `/api/calendar`) and Workflow destination routing.

### Frontend (F1 – F5)
1. **Milestone F1**: TypeScript Contracts, API Clients & Review Outbox Triage Page (`/outbox`).
2. **Milestone F2**: Tailwind-Native CSS Grid Calendar Core & Views (Month, Week, Day).
3. **Milestone F3**: Schedule Manager Modal & Slot Rule Recurrence Form Modal (Start/End Time, Capacity, Cadence).
4. **Milestone F4**: Reorderable Input Queue Sidebar (`QueueSidebar.tsx`) & Drag-and-Drop Interaction.
5. **Milestone F5**: Composer Split Publishing Button & Workflow Destination Mode Selectors (`Manual`, `Automatic`, `Scheduled`).
