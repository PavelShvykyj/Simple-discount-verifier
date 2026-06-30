import { Component, computed, inject, signal } from '@angular/core';
import {
  IonButton,
  IonButtons,
  IonCard,
  IonCardContent,
  IonCardHeader,
  IonCardSubtitle,
  IonCardTitle,
  IonContent,
  IonHeader,
  IonNote,
  ModalController,
  IonTitle,
  IonToolbar,
} from '@ionic/angular/standalone';

import { PublicRedemptionFlowStore } from '../../model/redemption-flow.store';
import { SupportQrCodeComponent } from '../support-qr-code/support-qr-code.component';

@Component({
  selector: 'app-support-qr-dialog',
  imports: [
    IonButton,
    IonButtons,
    IonCard,
    IonCardContent,
    IonCardHeader,
    IonCardSubtitle,
    IonCardTitle,
    IonContent,
    IonHeader,
    IonNote,
    SupportQrCodeComponent,
    IonTitle,
    IonToolbar,
  ],
  templateUrl: './support-qr-dialog.component.html',
  styleUrl: './support-qr-dialog.component.scss',
})
export class SupportQrDialogComponent {
  private readonly flowState = signal<PublicRedemptionFlowStore | null>(null);
  private readonly modalController = inject(ModalController);
  protected readonly correlationId = computed(() => this.flowState()?.correlationId() ?? null);

  set redemptionFlowStore(flow: PublicRedemptionFlowStore) {
    this.flowState.set(flow);
  }

  protected close(): void {
    void this.modalController.dismiss();
  }
}
