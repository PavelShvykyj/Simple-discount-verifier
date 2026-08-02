import {
  AfterViewInit,
  Component,
  ElementRef,
  effect,
  inject,
  viewChild,
} from '@angular/core';
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
import { helpCircleOutline } from 'ionicons/icons';

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
import { TurnstileWidgetService } from '../../../shared/lib/turnstile/turnstile-widget.service';

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
    TurnstileWidgetService,
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
  private readonly turnstileHost = viewChild.required<ElementRef<HTMLDivElement>>('turnstileHost');
  private readonly nav = inject(PublicRedemptionNavService);
  private readonly turnstile = inject(TurnstileWidgetService);
  private readonly alertController = inject(AlertController);
  private readonly modalController = inject(ModalController);

  constructor() {
    addIcons({ helpCircleOutline });

    effect(() => this.flow.setTurnstileToken(this.turnstile.token()));
    effect(() => this.flow.setTurnstileRequired(this.turnstile.required()));
    effect(() => {
      this.flow.turnstileRefreshRequested();
      this.turnstile.refresh();
    });
  }

  ngAfterViewInit(): void {
    this.nav.setNav(this.flowNav());
    void this.turnstile.render(this.turnstileHost().nativeElement);
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
