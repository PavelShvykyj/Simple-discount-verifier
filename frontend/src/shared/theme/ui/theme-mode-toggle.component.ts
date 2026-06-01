import { Component, inject } from '@angular/core';
import { IonSegment, IonSegmentButton } from '@ionic/angular/standalone';
import { SegmentCustomEvent } from '@ionic/core';

import { ThemeMode, ThemeModeService } from '../theme-mode.service';

@Component({
  selector: 'app-theme-mode-toggle',
  imports: [IonSegment, IonSegmentButton],
  template: `
    <ion-segment
      [value]="themeMode.mode()"
      aria-label="Режим теми"
      (ionChange)="onThemeModeChange($event)"
    >
      <ion-segment-button value="system">Системна</ion-segment-button>
      <ion-segment-button value="light">Світла</ion-segment-button>
      <ion-segment-button value="dark">Темна</ion-segment-button>
    </ion-segment>
  `,
  styles: `
    :host {
      display: block;
    }

    ion-segment {
      width: 100%;
    }

    ion-segment-button {
      min-width: 0;
      font-size: 0.78rem;
    }
  `,
})
export class ThemeModeToggleComponent {
  protected readonly themeMode = inject(ThemeModeService);

  protected onThemeModeChange(event: SegmentCustomEvent): void {
    const value = event.detail.value;

    if (value === 'system' || value === 'light' || value === 'dark') {
      this.themeMode.setMode(value satisfies ThemeMode);
    }
  }
}
