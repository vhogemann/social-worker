import {
  faBluesky,
  faFacebook,
  faInstagram,
  faLinkedin,
  faXTwitter,
} from "@fortawesome/free-brands-svg-icons";
import type { IconDefinition } from "@fortawesome/fontawesome-svg-core";

export const PLATFORM_BADGE_COLORS: Record<string, string> = {
  Bluesky: "bg-sky-500/15 text-sky-400 border border-sky-500/30",
  Twitter: "bg-blue-400/15 text-blue-400 border border-blue-400/30",
  LinkedIn: "bg-blue-600/15 text-blue-300 border border-blue-600/30",
  Facebook: "bg-indigo-500/15 text-indigo-400 border border-indigo-500/30",
  Instagram: "bg-pink-500/15 text-pink-400 border border-pink-500/30",
};

export const PLATFORM_ICONS: Record<string, IconDefinition> = {
  Bluesky: faBluesky,
  Twitter: faXTwitter,
  LinkedIn: faLinkedin,
  Facebook: faFacebook,
  Instagram: faInstagram,
};
