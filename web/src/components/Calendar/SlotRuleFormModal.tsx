import React, { useState, useMemo, useEffect } from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faPlus, faSpinner, faClock, faCalendarDays } from "@fortawesome/free-solid-svg-icons";
import { Modal } from "../common/Modal";
import { Button } from "../common/Button";
import { Input } from "../common/Input";
import { AlertBanner } from "../common/AlertBanner";
import {
  createSlotRule,
  type SlotRecurrenceType,
  type ScheduleSlotRuleDto,
  type CreateSlotRuleRequest
} from "../../api/schedules";

export interface SlotRuleFormInitialValues {
  startTime?: string;
  endTime?: string;
  selectedDays?: number[];
  dayOfMonth?: number;
  monthOfYear?: number;
  recurrenceType?: SlotRecurrenceType;
  postCapacity?: number;
}

export interface SlotRuleFormModalProps {
  isOpen: boolean;
  onClose: () => void;
  scheduleId: string;
  onCreated: (rule: ScheduleSlotRuleDto) => void;
  initialValues?: SlotRuleFormInitialValues;
}

const DAYS_OF_WEEK = [
  { label: "Mon", value: 1 },
  { label: "Tue", value: 2 },
  { label: "Wed", value: 3 },
  { label: "Thu", value: 4 },
  { label: "Fri", value: 5 },
  { label: "Sat", value: 6 },
  { label: "Sun", value: 0 }
];

const MONTHS = [
  "January", "February", "March", "April", "May", "June",
  "July", "August", "September", "October", "November", "December"
];

export function calculatePacingPreview(startTime: string, endTime: string, capacity: number): {
  isValid: boolean;
  message: string;
  sampleTimes: string[];
} {
  const [startH, startM] = startTime.split(":").map(Number);
  const [endH, endM] = endTime.split(":").map(Number);

  if (isNaN(startH) || isNaN(startM) || isNaN(endH) || isNaN(endM)) {
    return { isValid: false, message: "Invalid time format.", sampleTimes: [] };
  }

  const startTotalMinutes = startH * 60 + startM;
  const endTotalMinutes = endH * 60 + endM;
  const duration = endTotalMinutes - startTotalMinutes;

  if (duration <= 0) {
    return {
      isValid: false,
      message: "End time must be strictly later than start time.",
      sampleTimes: []
    };
  }

  if (capacity <= 1) {
    return {
      isValid: true,
      message: `Publishes 1 post at ${startTime}`,
      sampleTimes: [startTime]
    };
  }

  const intervalMinutes = Math.floor(duration / capacity);
  const sampleTimes: string[] = [];

  for (let i = 0; i < capacity; i++) {
    const mins = startTotalMinutes + i * intervalMinutes;
    const h = String(Math.floor(mins / 60)).padStart(2, "0");
    const m = String(mins % 60).padStart(2, "0");
    sampleTimes.push(`${h}:${m}`);
  }

  return {
    isValid: true,
    message: `Publishes 1 post every ${intervalMinutes} minutes between ${startTime} and ${endTime}`,
    sampleTimes
  };
}

export const SlotRuleFormModal: React.FC<SlotRuleFormModalProps> = ({
  isOpen,
  onClose,
  scheduleId,
  onCreated,
  initialValues
}) => {
  const [recurrenceType, setRecurrenceType] = useState<SlotRecurrenceType>("Daily");
  const [startTime, setStartTime] = useState("09:00");
  const [endTime, setEndTime] = useState("10:00");
  const [postCapacity, setPostCapacity] = useState(4);
  const [selectedDays, setSelectedDays] = useState<number[]>([1, 2, 3, 4, 5]); // Mon-Fri
  const [dayOfMonth, setDayOfMonth] = useState<number>(1);
  const [monthOfYear, setMonthOfYear] = useState<number>(1);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (isOpen) {
      const rec = initialValues?.recurrenceType ?? (initialValues?.selectedDays?.length ? "Weekly" : "Daily");
      setRecurrenceType(rec);
      setStartTime(initialValues?.startTime ?? "09:00");
      setEndTime(initialValues?.endTime ?? "10:00");
      setPostCapacity(initialValues?.postCapacity ?? 4);
      setSelectedDays(initialValues?.selectedDays ?? [1, 2, 3, 4, 5]);
      setDayOfMonth(initialValues?.dayOfMonth ?? 1);
      setMonthOfYear(initialValues?.monthOfYear ?? 1);
      setError(null);
    }
  }, [isOpen, initialValues]);

  const pacing = useMemo(() => {
    return calculatePacingPreview(startTime, endTime, postCapacity);
  }, [startTime, endTime, postCapacity]);

  const toggleDay = (dayVal: number) => {
    if (selectedDays.includes(dayVal)) {
      if (selectedDays.length > 1) {
        setSelectedDays(selectedDays.filter((d) => d !== dayVal));
      }
    } else {
      setSelectedDays([...selectedDays, dayVal].sort((a, b) => a - b));
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!pacing.isValid) {
      setError(pacing.message);
      return;
    }

    if (recurrenceType === "Weekly" && selectedDays.length === 0) {
      setError("Please select at least one day of the week.");
      return;
    }

    setLoading(true);
    setError(null);

    const req: CreateSlotRuleRequest = {
      recurrenceType,
      startTime: `${startTime}:00`,
      endTime: `${endTime}:00`,
      postCapacity
    };

    if (recurrenceType === "Weekly") {
      req.daysOfWeek = selectedDays;
    } else if (recurrenceType === "Monthly") {
      req.dayOfMonth = Number(dayOfMonth);
    } else if (recurrenceType === "Yearly") {
      req.dayOfMonth = Number(dayOfMonth);
      req.monthOfYear = Number(monthOfYear);
    }

    try {
      const created = await createSlotRule(scheduleId, req);
      onCreated(created);
      onClose();
    } catch (err: any) {
      setError(err?.message || "Failed to create slot rule.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title="Add Publishing Slot Rule"
      description="Configure recurring windows where queued posts will be published at equal intervals."
      maxWidth="lg"
      testId="slot-rule-modal"
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={loading}>
            Cancel
          </Button>
          <Button
            variant="primary"
            icon={loading ? faSpinner : faPlus}
            disabled={loading || !pacing.isValid}
            onClick={handleSubmit}
          >
            Create Slot Rule
          </Button>
        </>
      }
    >
      <form onSubmit={handleSubmit} className="space-y-4 font-sans">
        {error && <AlertBanner variant="error" message={error} />}

        {/* Recurrence Cadence */}
        <div>
          <label className="block text-[10px] font-mono uppercase tracking-wider text-muted mb-1.5">
            Recurrence Cadence
          </label>
          <div className="grid grid-cols-4 gap-2">
            {(["Daily", "Weekly", "Monthly", "Yearly"] as SlotRecurrenceType[]).map((type) => (
              <button
                key={type}
                type="button"
                data-testid={`recurrence-option-${type.toLowerCase()}`}
                onClick={() => setRecurrenceType(type)}
                className={`py-2 px-3 rounded-lg border text-xs font-mono transition text-center ${
                  recurrenceType === type
                    ? "border-accent bg-accent/10 text-accent font-semibold"
                    : "border-border bg-bg text-muted hover:text-foreground hover:bg-zinc-800/40"
                }`}
              >
                {type}
              </button>
            ))}
          </div>
        </div>

        {/* Weekly Day Selector */}
        {recurrenceType === "Weekly" && (
          <div>
            <label className="block text-[10px] font-mono uppercase tracking-wider text-muted mb-1.5">
              Days of the Week
            </label>
            <div className="flex gap-1.5 flex-wrap">
              {DAYS_OF_WEEK.map((day) => {
                const isSelected = selectedDays.includes(day.value);
                return (
                  <button
                    key={day.value}
                    type="button"
                    data-testid={`day-toggle-${day.label.toLowerCase()}`}
                    onClick={() => toggleDay(day.value)}
                    className={`w-10 h-8 rounded-lg border text-xs font-mono transition flex items-center justify-center ${
                      isSelected
                        ? "border-accent bg-accent text-white font-bold"
                        : "border-border bg-bg text-muted hover:text-foreground"
                    }`}
                  >
                    {day.label}
                  </button>
                );
              })}
            </div>
          </div>
        )}

        {/* Monthly Day Selector */}
        {recurrenceType === "Monthly" && (
          <div>
            <label className="block text-[10px] font-mono uppercase tracking-wider text-muted mb-1.5">
              Day of Month (1 - 31)
            </label>
            <input
              type="number"
              min={1}
              max={31}
              data-testid="day-of-month-input"
              value={dayOfMonth}
              onChange={(e) => setDayOfMonth(Number(e.target.value))}
              className="w-full rounded-lg border border-border bg-bg px-3 py-2 text-xs font-mono text-foreground outline-none focus:border-accent"
            />
          </div>
        )}

        {/* Yearly Month & Day Selector */}
        {recurrenceType === "Yearly" && (
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-[10px] font-mono uppercase tracking-wider text-muted mb-1.5">
                Month
              </label>
              <select
                value={monthOfYear}
                data-testid="month-of-year-select"
                onChange={(e) => setMonthOfYear(Number(e.target.value))}
                className="w-full rounded-lg border border-border bg-bg px-3 py-2 text-xs font-mono text-foreground outline-none focus:border-accent"
              >
                {MONTHS.map((m, idx) => (
                  <option key={m} value={idx + 1}>
                    {m}
                  </option>
                ))}
              </select>
            </div>
            <div>
              <label className="block text-[10px] font-mono uppercase tracking-wider text-muted mb-1.5">
                Day
              </label>
              <input
                type="number"
                min={1}
                max={31}
                data-testid="yearly-day-input"
                value={dayOfMonth}
                onChange={(e) => setDayOfMonth(Number(e.target.value))}
                className="w-full rounded-lg border border-border bg-bg px-3 py-2 text-xs font-mono text-foreground outline-none focus:border-accent"
              />
            </div>
          </div>
        )}

        {/* Time Window (Start Time & End Time) */}
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="block text-[10px] font-mono uppercase tracking-wider text-muted mb-1.5">
              Start Time
            </label>
            <input
              type="time"
              data-testid="start-time-input"
              required
              value={startTime}
              onChange={(e) => setStartTime(e.target.value)}
              className="w-full rounded-lg border border-border bg-bg px-3 py-2 text-xs font-mono text-foreground outline-none focus:border-accent"
            />
          </div>

          <div>
            <label className="block text-[10px] font-mono uppercase tracking-wider text-muted mb-1.5">
              End Time
            </label>
            <input
              type="time"
              data-testid="end-time-input"
              required
              value={endTime}
              onChange={(e) => setEndTime(e.target.value)}
              className="w-full rounded-lg border border-border bg-bg px-3 py-2 text-xs font-mono text-foreground outline-none focus:border-accent"
            />
          </div>
        </div>

        {/* Post Capacity */}
        <div>
          <div className="flex items-center justify-between mb-1.5">
            <label className="text-[10px] font-mono uppercase tracking-wider text-muted">
              Slot Window Post Capacity
            </label>
            <span className="text-xs font-mono font-bold text-accent">
              {postCapacity} {postCapacity === 1 ? "post" : "posts"}
            </span>
          </div>
          <input
            type="range"
            min={1}
            max={10}
            step={1}
            data-testid="capacity-slider"
            value={postCapacity}
            onChange={(e) => setPostCapacity(Number(e.target.value))}
            className="w-full accent-accent cursor-pointer"
          />
          <div className="flex justify-between text-[10px] font-mono text-muted/60 mt-1">
            <span>1 (Single post)</span>
            <span>5 posts</span>
            <span>10 posts</span>
          </div>
        </div>

        {/* Live Pacing Preview */}
        <div
          data-testid="pacing-preview"
          className={`p-3 rounded-lg border text-xs font-mono transition ${
            pacing.isValid
              ? "bg-accent/5 border-accent/30 text-foreground"
              : "bg-red-500/5 border-red-500/30 text-red-400"
          }`}
        >
          <div className="flex items-center gap-2 font-semibold mb-1">
            <FontAwesomeIcon
              icon={pacing.isValid ? faClock : faCalendarDays}
              className={pacing.isValid ? "text-accent" : "text-red-400"}
            />
            <span>Pacing Preview</span>
          </div>
          <p className="text-[11px] text-muted mb-2">{pacing.message}</p>

          {pacing.isValid && pacing.sampleTimes.length > 0 && (
            <div className="flex items-center gap-1.5 flex-wrap">
              {pacing.sampleTimes.map((time, idx) => (
                <span
                  key={idx}
                  className="px-2 py-0.5 rounded bg-zinc-800/80 border border-border text-[10px] text-accent font-semibold"
                >
                  #{idx + 1}: {time}
                </span>
              ))}
            </div>
          )}
        </div>
      </form>
    </Modal>
  );
};
