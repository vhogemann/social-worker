import typography from "@tailwindcss/typography";

/** @type {import('tailwindcss').Config} */
export default {
  content: ["./index.html", "./src/**/*.{ts,tsx}"],
  theme: {
    extend: {
      colors: {
        bg: "#0f1115",
        panel: "#161922",
        "panel-light": "#1d212d",
        border: "#262a36",
        "border-light": "#33394a",
        accent: "#7c8cff",
        "accent-foreground": "#0f1115",
        muted: "#8b909f",
        foreground: "#f4f4f6",
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

