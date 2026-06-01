import { DOCUMENT } from '@angular/common';
import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import {
  IonButton,
  IonContent,
  IonHeader,
  IonTitle,
  IonToolbar,
} from '@ionic/angular/standalone';

import { ThemeModeToggleComponent } from '../../../shared/theme/ui/theme-mode-toggle.component';

@Component({
  selector: 'app-home-page',
  imports: [IonButton, IonContent, IonHeader, IonTitle, IonToolbar, ThemeModeToggleComponent],
  templateUrl: './home.page.html',
  styleUrl: './home.page.scss',
})
export class HomePage {
  private readonly document = inject(DOCUMENT);
  private readonly router = inject(Router);

  protected openScannerSurvey(): void {
    this.blurFocusedElement();
    void this.router.navigateByUrl('/scanner-survey');
  }

  private blurFocusedElement(): void {
    let activeElement = this.document.activeElement;

    while (activeElement instanceof HTMLElement && activeElement.shadowRoot?.activeElement) {
      activeElement = activeElement.shadowRoot.activeElement;
    }

    if (activeElement instanceof HTMLElement) {
      activeElement.blur();
    }
  }
}
