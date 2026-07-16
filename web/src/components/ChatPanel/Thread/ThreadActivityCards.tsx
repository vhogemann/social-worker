import type { ChatActivityCard } from "../../../store/chatStore";

export function ThreadActivityCards({ cards }: { cards: ChatActivityCard[] }) {
  if (cards.length === 0) {
    return null;
  }

  return (
    <div className="px-4 py-2 border-b border-border" data-testid="thread-activity-cards">
      {cards.map((card) => (
        <div
          key={card.id}
          className={card.kind === "error"
            ? "my-1 px-2 py-1 text-xs font-mono rounded border border-red-400 text-red-300 bg-bg"
            : "my-1 px-2 py-1 text-xs font-mono rounded border border-border text-accent bg-bg"}
          data-testid="thread-activity-card"
        >
          {`activity: ${card.title} — ${card.message}`}
        </div>
      ))}
    </div>
  );
}
