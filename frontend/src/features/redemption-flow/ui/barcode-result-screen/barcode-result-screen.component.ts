import { Component, inject } from '@angular/core';
import {
  IonButton,
  IonButtons,
  IonCard,
  IonCardContent,
  IonCardHeader,
  IonCardSubtitle,
  IonCardTitle,
  IonItem,
  IonLabel,
  IonText,
} from '@ionic/angular/standalone';

import { PublicRedemptionNavService } from '../../navigation/public-redemption-nav.service';
import { PUBLIC_REDEMPTION_FLOW_STORE } from '../../model/redemption-flow.store';
import { MobileFlowScreenComponent } from '../../../../shared/ui/mobile-flow-screen/mobile-flow-screen.component';
import { DisabledButtonColorDirective } from '../../../../shared/ui/disabled-button-color/disabled-button-color.directive';

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
    IonItem,
    IonLabel,
    IonText,
    DisabledButtonColorDirective,
    MobileFlowScreenComponent,
  ],
  templateUrl: './barcode-result-screen.component.html',
})
export class BarcodeResultScreenComponent {
  protected readonly flow = inject(PUBLIC_REDEMPTION_FLOW_STORE);

  protected readonly nav = inject(PublicRedemptionNavService);

  protected restart(): void {
    this.flow.restart();
    void this.nav.resetToPhone();
  }

  protected goBack(): void {
    void this.nav.goBack();
  }
}
