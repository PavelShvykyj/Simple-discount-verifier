import { DOCUMENT } from '@angular/common';
import { Injectable, inject, signal } from '@angular/core';

export type ThemeMode = 'system' | 'light' | 'dark';

const STORAGE_KEY = 'simple-discount-verifier.theme-mode';
const LIGHT_THEME_COLOR = '#1252a3';
const DARK_THEME_COLOR = '#1a2027';

@Injectable({
  providedIn: 'root',
})
export class ThemeModeService {
  private readonly document = inject(DOCUMENT);

  readonly mode = signal<ThemeMode>(this.readStoredMode());

  private readonly systemDarkQuery =
    typeof window === 'undefined' ? undefined : window.matchMedia('(prefers-color-scheme: dark)');

  constructor() {
    this.applyMode(this.mode());
    this.systemDarkQuery?.addEventListener('change', () => {
      if (this.mode() === 'system') {
        this.applyMode('system');
      }
    });
  }

  setMode(mode: ThemeMode): void {
    this.mode.set(mode);
    localStorage.setItem(STORAGE_KEY, mode);
    this.applyMode(mode);
  }

  private applyMode(mode: ThemeMode): void {
    const shouldUseDark = mode === 'dark' || (mode === 'system' && this.systemDarkQuery?.matches);

    this.document.documentElement.classList.toggle('ion-palette-dark', Boolean(shouldUseDark));
    this.updateThemeColor(Boolean(shouldUseDark));
  }

  private updateThemeColor(shouldUseDark: boolean): void {
    this.document
      .querySelector<HTMLMetaElement>('meta[name="theme-color"]')
      ?.setAttribute('content', shouldUseDark ? DARK_THEME_COLOR : LIGHT_THEME_COLOR);
  }

  private readStoredMode(): ThemeMode {
    if (typeof localStorage === 'undefined') {
      return 'system';
    }

    const storedMode = localStorage.getItem(STORAGE_KEY);

    return storedMode === 'light' || storedMode === 'dark' || storedMode === 'system'
      ? storedMode
      : 'system';
  }
}
