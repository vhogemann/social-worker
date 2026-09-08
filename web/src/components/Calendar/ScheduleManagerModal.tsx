import React, { useEffect, useState, useCallback } from "react";
import {
  faPlus,
  faTrash,
  faSpinner,
  faCheck
} from "@fortawesome/free-solid-svg-icons";
import { Modal } from "../common/Modal";
import { Button } from "../common/Button";
import { Input } from "../common/Input";
import { AlertBanner } from "../common/AlertBanner";
import { ConfirmModal } from "../common/ConfirmModal";
import {
  fetchSchedules,
  fetchSchedule,
  createSchedule,
  updateSchedule,
  deleteSchedule,
  type ScheduleSummaryDto,
  type ScheduleDetailDto
} from "../../api/schedules";

export interface ScheduleManagerModalProps {
  isOpen: boolean;
  onClose: () => void;
  activeScheduleId?: string;
  onSchedulesChanged: (selectedScheduleId?: string) => void;
}

const COMMON_TIMEZONES = [
  "UTC",
  "Europe/London",
  "Europe/Paris",
  "Europe/Berlin",
  "America/New_York",
  "America/Chicago",
  "America/Denver",
  "America/Los_Angeles",
  "America/Toronto",
  "America/Sao_Paulo",
  "Asia/Tokyo",
  "Asia/Singapore",
  "Asia/Dubai",
  "Australia/Sydney",
  "Pacific/Auckland"
];

export const ScheduleManagerModal: React.FC<ScheduleManagerModalProps> = ({
  isOpen,
  onClose,
  activeScheduleId,
  onSchedulesChanged
}) => {
  const [schedules, setSchedules] = useState<ScheduleSummaryDto[]>([]);
  const [selectedId, setSelectedId] = useState<string>(activeScheduleId || "");
  const [detail, setDetail] = useState<ScheduleDetailDto | null>(null);
  const [isCreatingNew, setIsCreatingNew] = useState(false);

  // Form states
  const [name, setName] = useState("");
  const [timezone, setTimezone] = useState("UTC");
  const [isActive, setIsActive] = useState(true);

  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Child modals
  const [deletingSchedule, setDeletingSchedule] = useState(false);

  const loadSchedules = useCallback(async (targetId?: string) => {
    setLoading(true);
    setError(null);
    try {
      const list = await fetchSchedules();
      setSchedules(list);
      const nextId = targetId || selectedId || (list.length > 0 ? list[0].id : "");
      setSelectedId(nextId);

      if (nextId) {
        const d = await fetchSchedule(nextId);
        setDetail(d);
        setName(d.name);
        setTimezone(d.timezone);
        setIsActive(d.isActive);
        setIsCreatingNew(false);
      } else {
        setDetail(null);
        setIsCreatingNew(true);
      }
    } catch (err: any) {
      setError(err?.message || "Failed to load schedules.");
    } finally {
      setLoading(false);
    }
  }, [selectedId]);

  useEffect(() => {
    if (isOpen) {
      loadSchedules(activeScheduleId);
    }
  }, [isOpen, activeScheduleId, loadSchedules]);

  const handleSelectSchedule = async (id: string) => {
    setSelectedId(id);
    setIsCreatingNew(false);
    setError(null);
    try {
      const d = await fetchSchedule(id);
      setDetail(d);
      setName(d.name);
      setTimezone(d.timezone);
      setIsActive(d.isActive);
    } catch (err: any) {
      setError(err?.message || "Failed to load schedule details.");
    }
  };

  const handleStartCreateNew = () => {
    setIsCreatingNew(true);
    setSelectedId("");
    setDetail(null);
    setName("");
    setTimezone(Intl.DateTimeFormat().resolvedOptions().timeZone || "UTC");
    setIsActive(true);
    setError(null);
  };

  const handleSaveSchedule = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) {
      setError("Schedule name is required.");
      return;
    }

    setSaving(true);
    setError(null);

    try {
      if (isCreatingNew) {
        const created = await createSchedule({ name: name.trim(), timezone });
        await loadSchedules(created.id);
        onSchedulesChanged(created.id);
      } else if (selectedId) {
        await updateSchedule(selectedId, { name: name.trim(), timezone, isActive });
        await loadSchedules(selectedId);
        onSchedulesChanged(selectedId);
      }
    } catch (err: any) {
      setError(err?.message || "Failed to save schedule.");
    } finally {
      setSaving(false);
    }
  };

  const handleDeleteScheduleConfirm = async () => {
    if (!selectedId) return;
    setSaving(true);
    setError(null);
    try {
      await deleteSchedule(selectedId);
      setDeletingSchedule(false);
      await loadSchedules();
      onSchedulesChanged();
    } catch (err: any) {
      setError(err?.message || "Failed to delete schedule.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <>
      <Modal
        isOpen={isOpen}
        onClose={onClose}
        title="Calendar Settings"
        description="Configure publishing schedule name, timezone, and active status."
        maxWidth="xl"
        testId="schedule-manager-modal"
        footer={
          <Button variant="secondary" onClick={onClose}>
            Close
          </Button>
        }
      >
        <div className="flex flex-col gap-5">
          {error && <AlertBanner variant="error" message={error} />}

          {/* Schedule Picker Bar */}
          <div className="flex items-center justify-between gap-3 pb-3 border-b border-border flex-wrap">
            <div className="flex items-center gap-2 flex-wrap">
              <span className="text-xs font-mono text-muted">Select Schedule:</span>
              <div className="flex gap-1.5 flex-wrap">
                {schedules.map((s) => (
                  <button
                    key={s.id}
                    type="button"
                    data-testid={`schedule-tab-${s.id}`}
                    onClick={() => handleSelectSchedule(s.id)}
                    className={`px-3 py-1 rounded-lg text-xs font-mono transition ${
                      !isCreatingNew && selectedId === s.id
                        ? "bg-accent text-white font-semibold"
                        : "bg-card border border-border text-muted hover:text-foreground hover:bg-zinc-800"
                    }`}
                  >
                    {s.name}
                  </button>
                ))}
              </div>
            </div>

            <Button
              size="sm"
              variant="secondary"
              icon={faPlus}
              data-testid="create-new-schedule-btn"
              onClick={handleStartCreateNew}
            >
              New Schedule
            </Button>
          </div>

          {/* Schedule Edit / Create Form */}
          <form onSubmit={handleSaveSchedule} className="space-y-4">
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <Input
                label="Schedule Name"
                placeholder="e.g. Daily Tech Beat"
                value={name}
                onChange={(e) => setName(e.target.value)}
                required
                data-testid="schedule-name-input"
              />

              <div>
                <label className="block text-[10px] font-mono uppercase tracking-wider text-muted mb-1.5">
                  Timezone (IANA)
                </label>
                <select
                  value={timezone}
                  onChange={(e) => setTimezone(e.target.value)}
                  data-testid="schedule-timezone-select"
                  className="w-full rounded-lg border border-border bg-bg px-3 py-2 text-xs font-mono text-foreground outline-none focus:border-accent"
                >
                  {COMMON_TIMEZONES.map((tz) => (
                    <option key={tz} value={tz}>
                      {tz}
                    </option>
                  ))}
                  {!COMMON_TIMEZONES.includes(timezone) && (
                    <option value={timezone}>{timezone}</option>
                  )}
                </select>
              </div>
            </div>

            {!isCreatingNew && (
              <div className="flex items-center gap-2 pt-1">
                <input
                  type="checkbox"
                  id="schedule-is-active"
                  checked={isActive}
                  onChange={(e) => setIsActive(e.target.checked)}
                  className="rounded border-border text-accent focus:ring-accent"
                />
                <label htmlFor="schedule-is-active" className="text-xs font-mono text-muted cursor-pointer">
                  Schedule is active and publishing
                </label>
              </div>
            )}

            <div className="flex items-center justify-between pt-2">
              <div>
                {!isCreatingNew && selectedId && (
                  <Button
                    type="button"
                    variant="danger"
                    size="sm"
                    icon={faTrash}
                    data-testid="delete-schedule-btn"
                    onClick={() => setDeletingSchedule(true)}
                  >
                    Delete Schedule
                  </Button>
                )}
              </div>

              <Button
                type="submit"
                variant="primary"
                size="sm"
                icon={saving ? faSpinner : faCheck}
                disabled={saving || !name.trim()}
                data-testid="save-schedule-btn"
              >
                {isCreatingNew ? "Create Schedule" : "Save Changes"}
              </Button>
            </div>
          </form>
        </div>
      </Modal>

      {/* Child: Confirm Delete Schedule Modal */}
      <ConfirmModal
        isOpen={deletingSchedule}
        title="Delete Schedule"
        message={`Are you sure you want to delete schedule "${name}"? All associated slot rules and queue mappings will be removed.`}
        confirmLabel="Delete Schedule"
        variant="danger"
        loading={saving}
        onConfirm={handleDeleteScheduleConfirm}
        onCancel={() => setDeletingSchedule(false)}
      />
    </>
  );
};
