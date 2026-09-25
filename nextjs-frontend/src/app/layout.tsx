import type { Metadata } from "next";
import { Fira_Sans, Fira_Code } from "next/font/google";
import { Providers } from "./providers";
import { ErrorBoundary } from "@/components/ErrorBoundary";
import "./globals.css";

const firaSans = Fira_Sans({
  variable: "--font-fira-sans",
  weight: ["300", "400", "500", "600", "700"],
  subsets: ["latin"],
});

const firaCode = Fira_Code({
  variable: "--font-fira-code",
  weight: ["400", "500", "600", "700"],
  subsets: ["latin"],
});

export const metadata: Metadata = {
  title: "NEXTERP - Enterprise Resource Planning",
  description: "Comprehensive ERP solution for modern enterprises",
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="en" className="h-full" suppressHydrationWarning>
      <body className={`${firaSans.variable} ${firaCode.variable} font-sans antialiased h-full`}>
        {/* Applies the stored/system theme before hydration so dark: utilities
            (class-based, see globals.css) render correctly on first paint on
            every route - not just pages that mount ThemeToggle - and without
            a light-then-dark flash. Must be an inline script (not an
            imported function) so it runs synchronously before React
            hydrates; keep this logic in sync with applyTheme() in
            ThemeToggle.tsx if the theme rules ever change. */}
        <script
          dangerouslySetInnerHTML={{
            __html: `(function(){try{var t=localStorage.getItem('nexterp-theme')||'system';var d=t==='dark'||(t==='system'&&window.matchMedia('(prefers-color-scheme: dark)').matches);document.documentElement.classList.toggle('dark',d);}catch(e){}})();`,
          }}
        />
        <ErrorBoundary>
          <Providers>{children}</Providers>
        </ErrorBoundary>
      </body>
    </html>
  );
}
