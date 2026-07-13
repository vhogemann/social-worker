const SLASH_CATEGORIES = [
  { id: "general", label: "General", type: "category" as const },
  { id: "draft", label: "Draft", type: "category" as const },
  { id: "sources", label: "Sources", type: "category" as const },
  { id: "media", label: "Media", type: "category" as const },
  { id: "variants", label: "Variants", type: "category" as const },
  { id: "publishing", label: "Publishing", type: "category" as const },
];

const SLASH_ITEMS = [
  {
    id: "help-list",
    type: "command",
    categoryId: "general",
    label: "/help",
    description: "Show available slash commands",
    metadata: { command: "/help" },
  },
  {
    id: "validate-draft",
    type: "command",
    categoryId: "draft",
    label: "/validate",
    description: "Run draft validation immediately",
    metadata: { command: "/validate" },
  },
  {
    id: "web-search",
    type: "command",
    categoryId: "sources",
    label: "/search",
    description: "Search the web by keyword",
    metadata: { command: "/search " },
  },
  {
    id: "image-search",
    type: "command",
    categoryId: "media",
    label: "/search-image",
    description: "Search for images by keyword",
    metadata: { command: "/search-image " },
  },
];

export const slashAdapter = {
  categories: () => SLASH_CATEGORIES,
  categoryItems: (categoryId: string) =>
    SLASH_ITEMS.filter((item) => item.categoryId === categoryId),
  search: (query: string) => {
    const q = query.trim().toLowerCase();
    if (!q) {
      return SLASH_ITEMS;
    }

    return SLASH_ITEMS.filter((item) => {
      return (
        item.label.toLowerCase().includes(q) ||
        (item.description?.toLowerCase().includes(q) ?? false)
      );
    });
  },
};
