import { AfterViewInit, Component, inject, viewChild } from '@angular/core';
import {
  AlertController,
  IonButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonIcon,
  ModalController,
  IonNav,
  IonTitle,
  IonToolbar,
} from '@ionic/angular/standalone';
import { addIcons } from 'ionicons';
import { qrCodeOutline } from 'ionicons/icons';

import { PublicRedemptionApi } from '../../../features/redemption-flow/api/public-redemption.api';
import { PublicRedemptionNavService } from '../../../features/redemption-flow/navigation/public-redemption-nav.service';
import {
  PUBLIC_REDEMPTION_FLOW_STORE,
  PublicRedemptionSignalStore,
} from '../../../features/redemption-flow/model/redemption-flow.store';
import { isSupportCorrelationId } from '../../../features/redemption-flow/model/support-qr-payload';
import { PhoneEntryScreenComponent } from '../../../features/redemption-flow/ui/phone-entry-screen/phone-entry-screen.component';
import { SupportQrDialogComponent } from '../../../features/redemption-flow/ui/support-qr-dialog/support-qr-dialog.component';
import { ThemeModeSelectorComponent } from '../../../shared/theme/ui/theme-mode-selector.component';

const PRE_CORRELATION_SUPPORT_MESSAGE =
  'Код підтримки з\'явиться після початку перевірки телефону. До цього моменту зверніться до працівника напряму.';
const INVALID_CORRELATION_SUPPORT_MESSAGE =
  'Код підтримки недоступний для цього стану перевірки. Спробуйте почати перевірку знову або зверніться до працівника.';

@Component({
  selector: 'app-public-redemption-page',
  imports: [
    IonButton,
    IonButtons,
    IonContent,
    IonHeader,
    IonIcon,
    IonNav,
    IonTitle,
    IonToolbar,
    ThemeModeSelectorComponent,
  ],
  providers: [
    PublicRedemptionApi,
    PublicRedemptionNavService,
    PublicRedemptionSignalStore,
    {
      provide: PUBLIC_REDEMPTION_FLOW_STORE,
      useExisting: PublicRedemptionSignalStore,
    },
  ],
  templateUrl: './public-redemption.page.html',
  styleUrl: './public-redemption.page.scss',
})
export class PublicRedemptionPage implements AfterViewInit {
  protected readonly phoneEntryScreen = PhoneEntryScreenComponent;
  protected readonly flow = inject(PUBLIC_REDEMPTION_FLOW_STORE);

  private readonly flowNav = viewChild.required<IonNav>('flowNav');
  private readonly nav = inject(PublicRedemptionNavService);
  private readonly alertController = inject(AlertController);
  private readonly modalController = inject(ModalController);

  constructor() {
    addIcons({ qrCodeOutline });
  }

  ngAfterViewInit(): void {
    this.nav.setNav(this.flowNav());
  }

  protected async openSupport(): Promise<void> {
    const correlationId = this.flow.correlationId();

    if (correlationId === null) {
      await this.presentSupportInfo(PRE_CORRELATION_SUPPORT_MESSAGE);
      return;
    }

    if (!isSupportCorrelationId(correlationId)) {
      await this.presentSupportInfo(INVALID_CORRELATION_SUPPORT_MESSAGE);
      return;
    }

    await this.presentSupportQr();
  }

  private async presentSupportInfo(message: string): Promise<void> {
    const alert = await this.alertController.create({
      header: 'Код підтримки',
      message,
      buttons: ['Зрозуміло'],
    });

    await alert.present();
  }

  private async presentSupportQr(): Promise<void> {
    const modal = await this.modalController.create({
      component: SupportQrDialogComponent,
      componentProps: {
        redemptionFlowStore: this.flow,
      },
    });

    await modal.present();
  }
}
