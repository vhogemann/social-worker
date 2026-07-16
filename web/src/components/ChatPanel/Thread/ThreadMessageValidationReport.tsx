import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faCircleCheck,
  faCircleInfo,
  faHashtag,
  faTriangleExclamation,
  faLink,
  faImage,
  faFont,
} from "@fortawesome/free-solid-svg-icons";

type ValidationChipKind =
  | "count"
  | "error"
  | "warning"
  | "status-valid"
  | "status-failed"
  | "status-warning";

type ValidationRuleKind =
  | "char-limit"
  | "markdown"
  | "placeholder-url"
  | "placeholder-media"
  | "style"
  | "generic";

export type ValidationChip = {
  text: string;
  kind: ValidationChipKind;
  ruleKind?: ValidationRuleKind;
};

export type ParsedValidationReport = {
  chips: ValidationChip[];
};

function cleanLine(line: string): string {
  return line
    .replace(/^\s*[-*]\s*/, "")
    .replace(/^\s*#{1,6}\s*/, "")
    .replace(/\*\*/g, "")
    .replace(/^[^\p{L}\p{N}]+/u, "")
    .trim();
}

export function parseValidationReport(text: string): ParsedValidationReport | null {
  if (!/Draft Validation Report|Overall Status|Character Count/i.test(text)) {
    return null;
  }

  const chips: ValidationChip[] = [];
  let currentPostLabel = "";

  for (const rawLine of text.split("\n")) {
    const line = cleanLine(rawLine);
    if (!line) {
      continue;
    }

    const postMatch = line.match(/^Post\s+(\d+)\s*:/i);
    if (postMatch) {
      currentPostLabel = `post ${postMatch[1]}: `;
      continue;
    }

    if (/^Character Count\s*:/i.test(line)) {
      chips.push({
        text: `${currentPostLabel}${line}`,
        kind: "count",
      });
      continue;
    }

    if (/Error\s*:/i.test(line) || /Warning\s*:/i.test(line)) {
      const isWarning = /Warning\s*:/i.test(line);
      chips.push({
        text: `${currentPostLabel}${line}`,
        kind: isWarning ? "warning" : "error",
        ruleKind: classifyRule(line),
      });
      continue;
    }

    if (/Overall Status\s*:/i.test(line)) {
      chips.push({
        text: line,
        kind: classifyStatus(line),
      });
    }
  }

  if (chips.length === 0) {
    return null;
  }

  return { chips };
}

function classifyStatus(line: string): ValidationChipKind {
  if (/valid\./i.test(line) || /overall status\s*:\s*valid/i.test(line)) {
    return "status-valid";
  }

  if (/warning/i.test(line)) {
    return "status-warning";
  }

  return "status-failed";
}

function classifyRule(line: string): ValidationRuleKind {
  if (/300-character limit|character limit|exceeds/i.test(line)) {
    return "char-limit";
  }

  if (/unsupported markdown|bold\/italic\/heading/i.test(line)) {
    return "markdown";
  }

  if (/placeholder url|example\.com/i.test(line)) {
    return "placeholder-url";
  }

  if (/placeholder media|media:\/\//i.test(line)) {
    return "placeholder-media";
  }

  if (/title-like opener|conversational opening/i.test(line)) {
    return "style";
  }

  return "generic";
}

function getRuleBadge(ruleKind?: ValidationRuleKind): string | null {
  if (!ruleKind || ruleKind === "generic") {
    return null;
  }

  if (ruleKind === "char-limit") return "length";
  if (ruleKind === "markdown") return "markdown";
  if (ruleKind === "placeholder-url") return "url";
  if (ruleKind === "placeholder-media") return "media";
  if (ruleKind === "style") return "style";
  return null;
}

function getChipVisual(chip: ValidationChip) {
  if (chip.kind === "count") {
    return {
      container: "my-1 px-2 py-1 text-xs font-mono rounded border border-sky-500/40 text-sky-200 bg-sky-950/20",
      icon: faHashtag,
    };
  }

  if (chip.kind === "warning") {
    return {
      container: "my-1 px-2 py-1 text-xs font-mono rounded border border-amber-500/40 text-amber-200 bg-amber-950/20",
      icon: faCircleInfo,
    };
  }

  if (chip.kind === "status-valid") {
    return {
      container: "my-1 px-2 py-1 text-xs font-mono rounded border border-emerald-500/40 text-emerald-200 bg-emerald-950/20",
      icon: faCircleCheck,
    };
  }

  if (chip.kind === "status-warning") {
    return {
      container: "my-1 px-2 py-1 text-xs font-mono rounded border border-amber-500/40 text-amber-200 bg-amber-950/20",
      icon: faCircleInfo,
    };
  }

  if (chip.kind === "status-failed") {
    return {
      container: "my-1 px-2 py-1 text-xs font-mono rounded border border-red-500/40 text-red-200 bg-red-950/20",
      icon: faTriangleExclamation,
    };
  }

  if (chip.ruleKind === "placeholder-url") {
    return {
      container: "my-1 px-2 py-1 text-xs font-mono rounded border border-red-500/40 text-red-200 bg-red-950/20",
      icon: faLink,
    };
  }

  if (chip.ruleKind === "placeholder-media") {
    return {
      container: "my-1 px-2 py-1 text-xs font-mono rounded border border-red-500/40 text-red-200 bg-red-950/20",
      icon: faImage,
    };
  }

  if (chip.ruleKind === "markdown") {
    return {
      container: "my-1 px-2 py-1 text-xs font-mono rounded border border-red-500/40 text-red-200 bg-red-950/20",
      icon: faFont,
    };
  }

  return {
    container: "my-1 px-2 py-1 text-xs font-mono rounded border border-red-500/40 text-red-200 bg-red-950/20",
    icon: faTriangleExclamation,
  };
}

export function ThreadMessageValidationReport({ chips }: { chips: ValidationChip[] }) {
  return (
    <div data-testid="validation-report-chips">
      {chips.map((chip, index) => (
        <div key={`${chip.text}-${index}`} className={getChipVisual(chip).container}>
          <div className="flex items-start gap-2">
            <FontAwesomeIcon icon={getChipVisual(chip).icon} className="mt-0.5" />
            <span className="flex-1">{chip.text}</span>
            {getRuleBadge(chip.ruleKind) && (
              <span className="px-1.5 py-0.5 rounded border border-current/30 text-[10px] uppercase tracking-wide">
                {getRuleBadge(chip.ruleKind)}
              </span>
            )}
          </div>
        </div>
      ))}
    </div>
  );
}