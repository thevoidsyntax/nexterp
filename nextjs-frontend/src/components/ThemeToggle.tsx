'use client';

import { useState } from 'react';
import { Sun, Moon, Monitor } from 'lucide-react';
import { cn } from '@/lib/utils';
import { useTheme, type Theme } from '@/hooks/useTheme';

interface ThemeToggleProps {
  className?: string;
}

const themeIcons: Record<Theme, typeof Sun> = {
  light: Sun,
  dark: Moon,
  system: Monitor,
};

const themeLabels: Record<Theme, string> = {
  light: 'Light',
  dark: 'Dark',
  system: 'System',
};

export function ThemeToggle({ className }: ThemeToggleProps) {
  const { theme, setTheme } = useTheme();
  const [isOpen, setIsOpen] = useState(false);

  const handleThemeChange = (newTheme: Theme) => {
    setTheme(newTheme);
    setIsOpen(false);
  };

  const CurrentIcon = themeIcons[theme];

  return (
    <div className={cn('relative', className)}>
      <button
        onClick={() => setIsOpen(!isOpen)}
        className="flex items-center gap-2 p-2 text-slate-600 dark:text-slate-400 hover:bg-surface-muted rounded-lg transition"
        aria-label={`Theme: ${themeLabels[theme]}`}
      >
        <CurrentIcon className="w-5 h-5" />
      </button>

      {isOpen && (
        <>
          {/* Backdrop */}
          <div
            className="fixed inset-0 z-40"
            onClick={() => setIsOpen(false)}
          />

          {/* Dropdown */}
          <div className="absolute right-0 mt-2 w-40 bg-surface border border-border-subtle rounded-lg shadow-lg z-50 overflow-hidden">
            <div className="p-1">
              {(['light', 'dark', 'system'] as Theme[]).map((t) => {
                const Icon = themeIcons[t];
                return (
                  <button
                    key={t}
                    onClick={() => handleThemeChange(t)}
                    className={cn(
                      'w-full flex items-center gap-2 px-3 py-2 text-sm rounded-md transition',
                      theme === t
                        ? 'bg-primary/10 text-primary'
                        : 'text-slate-600 dark:text-slate-400 hover:bg-surface-muted'
                    )}
                  >
                    <Icon className="w-4 h-4" />
                    {themeLabels[t]}
                    {theme === t && (
                      <Check className="w-4 h-4 ml-auto" />
                    )}
                  </button>
                );
              })}
            </div>
          </div>
        </>
      )}
    </div>
  );
}

function Check({ className }: { className?: string }) {
  return (
    <svg className={className} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
      <polyline points="20 6 9 17 4 12" />
    </svg>
  );
}
