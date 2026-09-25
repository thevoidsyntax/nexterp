'use client';

import { useCallback, useEffect, useState } from 'react';

export type Theme = 'light' | 'dark' | 'system';

const STORAGE_KEY = 'nexterp-theme';

function applyTheme(theme: Theme) {
  const root = document.documentElement;
  if (theme === 'system') {
    const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
    root.classList.toggle('dark', prefersDark);
  } else {
    root.classList.toggle('dark', theme === 'dark');
  }
}

// Every mounted useTheme() instance (ThemeToggle in the header, the
// Appearance tab in Settings, ...) registers here so that a theme change
// made through any one of them updates all the others immediately, instead
// of each instance holding its own out-of-sync local state.
const listeners = new Set<(theme: Theme) => void>();

export function useTheme() {
  const [theme, setThemeState] = useState<Theme>(() => {
    if (typeof window === 'undefined') return 'system';
    return (localStorage.getItem(STORAGE_KEY) as Theme | null) || 'system';
  });

  useEffect(() => {
    listeners.add(setThemeState);
    return () => { listeners.delete(setThemeState); };
  }, []);

  // Applies the theme to the DOM whenever it changes, and - only while
  // 'system' is selected - keeps it in sync with live OS scheme changes.
  useEffect(() => {
    applyTheme(theme);
    if (theme !== 'system') return;
    const media = window.matchMedia('(prefers-color-scheme: dark)');
    const onChange = () => applyTheme('system');
    media.addEventListener('change', onChange);
    return () => media.removeEventListener('change', onChange);
  }, [theme]);

  const setTheme = useCallback((next: Theme) => {
    localStorage.setItem(STORAGE_KEY, next);
    listeners.forEach((fn) => fn(next));
  }, []);

  return { theme, setTheme };
}
