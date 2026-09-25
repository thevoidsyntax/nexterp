interface LogoProps {
  className?: string;
  /** Renders only the amber "N" glyph, no background square - for placing on top of an existing colored surface (e.g. the sidebar's own navy background). */
  markOnly?: boolean;
}

/**
 * The NEXTERP mark: a bold geometric "N" (three solid bars, no rounding -
 * stays crisp down to 16px favicon size) on the app's navy/amber brand pair.
 * Single source of truth for the sidebar badge, the login screen, and the
 * generated favicon (scripts/generate-icons.mjs renders this same shape).
 */
export function Logo({ className, markOnly = false }: LogoProps) {
  return (
    <svg viewBox="0 0 128 128" className={className} role="img" aria-label="NEXTERP">
      {!markOnly && (
        <>
          <defs>
            <linearGradient id="nexterp-logo-bg" x1="0" y1="0" x2="128" y2="128" gradientUnits="userSpaceOnUse">
              <stop offset="0" stopColor="#16397a" />
              <stop offset="1" stopColor="#0f2a5c" />
            </linearGradient>
          </defs>
          <rect width="128" height="128" rx="28" fill="url(#nexterp-logo-bg)" />
        </>
      )}
      <rect x="24" y="24" width="16" height="80" fill="#f59e0b" />
      <rect x="88" y="24" width="16" height="80" fill="#f59e0b" />
      <polygon points="24,24 40,24 104,104 88,104" fill="#f59e0b" />
    </svg>
  );
}
