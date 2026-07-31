import { Component, inject } from '@angular/core';
import {
  IonButton,
  IonButtons,
  IonCard,
  IonCardContent,
  IonCardHeader,
  IonCardSubtitle,
  IonCardTitle,
  IonText,
} from '@ionic/angular/standalone';

import { PublicRedemptionNavService } from '../../navigation/public-redemption-nav.service';
import { PUBLIC_REDEMPTION_FLOW_STORE } from '../../model/redemption-flow.store';
import { MobileFlowScreenComponent } from '../../../../shared/ui/mobile-flow-screen/mobile-flow-screen.component';
import { DisabledButtonColorDirective } from '../../../../shared/ui/disabled-button-color/disabled-button-color.directive';
import { CountdownTimerComponent } from '../../../../shared/ui/countdown-timer/countdown-timer.component';
import { ConfirmActionSheetService } from '../../../../shared/ui/confirm-action-sheet/confirm-action-sheet.service';
import { BarcodeRendererComponent } from '../barcode-renderer/barcode-renderer.component';

@Component({
  selector: 'app-barcode-result-screen',
  imports: [
    IonButton,
    IonButtons,
    IonCard,
    IonCardContent,
    IonCardHeader,
    IonCardSubtitle,
    IonCardTitle,
    IonText,
    BarcodeRendererComponent,
    CountdownTimerComponent,
    DisabledButtonColorDirective,
    MobileFlowScreenComponent,
  ],
  templateUrl: './barcode-result-screen.component.html',
})
export class BarcodeResultScreenComponent {
  protected readonly flow = inject(PUBLIC_REDEMPTION_FLOW_STORE);

  protected readonly nav = inject(PublicRedemptionNavService);
  private readonly confirmActionSheet = inject(ConfirmActionSheetService);

  protected async requestNewCode(expiresAt: string): Promise<void> {
    if (isActiveUntil(expiresAt)) {
      const isConfirmed = await this.confirmActionSheet.confirm({
        header: 'Отримати повторний код?',
        message: 'Поточний код ще активний. Новий запит скасує його використання.',
        confirmText: 'Отримати повторний код',
        cancelText: 'Залишити поточний',
        importance: 'warning',
      });

      if (!isConfirmed) {
        return;
      }
    }

    this.restart();
  }

  protected restart(): void {
    this.flow.restart();
    void this.nav.resetToPhone();
  }

  protected goBack(): void {
    void this.nav.goBack();
  }
}

function isActiveUntil(expiresAt: string): boolean {
  const expiresAtTime = Date.parse(expiresAt);

  return !Number.isNaN(expiresAtTime) && expiresAtTime > Date.now();
}
