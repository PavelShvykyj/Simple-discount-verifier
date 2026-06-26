import { Component, computed, inject, signal } from '@angular/core';
import { IonActionSheet, IonButton, IonIcon } from '@ionic/angular/standalone';
import { ActionSheetButton } from '@ionic/core';
import { addIcons } from 'ionicons';
import { moonOutline, phonePortraitOutline, sunnyOutline } from 'ionicons/icons';

import { ThemeMode, ThemeModeService } from '../theme-mode.service';

interface ThemeModeOption {
  readonly value: ThemeMode;
  readonly label: string;
  readonly icon: string;
}

const THEME_MODE_OPTIONS: readonly ThemeModeOption[] = [
  {
    value: 'system',
    label: 'Системна',
    icon: 'phone-portrait-outline',
  },
  {
    value: 'light',
    label: 'Світла',
    icon: 'sunny-outline',
  },
  {
    value: 'dark',
    label: 'Темна',
    icon: 'moon-outline',
  },
];

@Component({
  selector: 'app-theme-mode-selector',
  imports: [IonActionSheet, IonButton, IonIcon],
  templateUrl: './theme-mode-selector.component.html',
  styleUrl: './theme-mode-selector.component.scss',
})
export class ThemeModeSelectorComponent {
  private readonly themeMode = inject(ThemeModeService);

  protected readonly isThemeSheetOpen = signal(false);
  protected readonly activeIcon = computed(() => this.activeOption().icon);
  protected readonly themeActions = computed<readonly ActionSheetButton[]>(() => [
    ...THEME_MODE_OPTIONS.map((option) => ({
      text:
        option.value === this.themeMode.mode()
          ? `${option.label} - обрано`
          : option.label,
      icon: option.icon,
      handler: () => {
        this.themeMode.setMode(option.value);
      },
    })),
    {
      text: 'Скасувати',
      role: 'cancel',
    },
  ]);

  constructor() {
    addIcons({ moonOutline, phonePortraitOutline, sunnyOutline });
  }

  protected openThemeSheet(): void {
    this.isThemeSheetOpen.set(true);
  }

  protected closeThemeSheet(): void {
    this.isThemeSheetOpen.set(false);
  }

  private activeOption(): ThemeModeOption {
    return (
      THEME_MODE_OPTIONS.find((option) => option.value === this.themeMode.mode()) ??
      THEME_MODE_OPTIONS[0]
    );
  }
}
