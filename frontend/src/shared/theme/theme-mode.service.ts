import { Injectable, signal } from '@angular/core';

export type ThemeMode = 'system' | 'light' | 'dark';

const STORAGE_KEY = 'simple-discount-verifier.theme-mode';

@Injectable({
  providedIn: 'root',
})
export class ThemeModeService {
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

    document.documentElement.classList.toggle('ion-palette-dark', Boolean(shouldUseDark));
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
