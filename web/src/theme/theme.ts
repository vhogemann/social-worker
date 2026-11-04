import { createTheme, responsiveFontSizes, type Theme } from "@mui/material/styles";

export type PaletteMode = "light" | "dark";

export function createAppTheme(mode: PaletteMode): Theme {
  let theme = createTheme({
    palette: {
      mode,
    },
    typography: {
      fontFamily: 'ui-sans-serif, system-ui, -apple-system, "Segoe UI", sans-serif',
      button: {
        textTransform: "none",
        fontWeight: 600,
      },
    },
    shape: {
      borderRadius: 8,
    },
    components: {
      MuiButton: {
        defaultProps: {
          disableElevation: true,
        },
      },
      MuiPaper: {
        defaultProps: {
          elevation: 0,
        },
      },
    },
  });

  return responsiveFontSizes(theme);
}
