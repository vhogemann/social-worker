import typography from "@tailwindcss/typography";

/** @type {import('tailwindcss').Config} */
export default {
  content: ["./index.html", "./src/**/*.{ts,tsx}"],
  theme: {
    extend: {
      colors: {
        bg: "var(--color-bg)",
        input: "var(--color-bg)",
        panel: "var(--color-panel)",
        "panel-light": "var(--color-panel-light)",
        card: "var(--color-card)",
        border: "var(--color-border)",
        "border-light": "var(--color-border-light)",
        accent: "var(--color-accent)",
        "accent-foreground": "var(--color-accent-foreground)",
        muted: "var(--color-muted)",
        foreground: "var(--color-foreground)",
        destructive: "var(--color-destructive)",
      },
      fontFamily: {
        mono: ["ui-monospace", "SFMono-Regular", "Menlo", "monospace"],
      },
      keyframes: {
        fadeIn: {
          "0%": { opacity: "0", transform: "scale(0.98)" },
          "100%": { opacity: "1", transform: "scale(1)" },
        },
      },
      animation: {
        "fade-in": "fadeIn 150ms ease-out",
        "spin-slow": "spin 1.5s linear infinite",
      },
    },
  },
  plugins: [typography],
};
