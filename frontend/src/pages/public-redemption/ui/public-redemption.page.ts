import { AfterViewInit, Component, inject, viewChild } from '@angular/core';
import { IonButtons, IonContent, IonHeader, IonNav, IonTitle, IonToolbar } from '@ionic/angular/standalone';

import { PublicRedemptionApi } from '../../../features/redemption-flow/api/public-redemption.api';
import { PublicRedemptionNavService } from '../../../features/redemption-flow/navigation/public-redemption-nav.service';
import {
  PUBLIC_REDEMPTION_FLOW_STORE,
  PublicRedemptionSignalStore,
} from '../../../features/redemption-flow/model/redemption-flow.store';
import { PhoneEntryScreenComponent } from '../../../features/redemption-flow/ui/phone-entry-screen/phone-entry-screen.component';
import { ThemeModeSelectorComponent } from '../../../shared/theme/ui/theme-mode-selector.component';

@Component({
  selector: 'app-public-redemption-page',
  imports: [IonButtons, IonContent, IonHeader, IonNav, IonTitle, IonToolbar, ThemeModeSelectorComponent],
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

  private readonly flowNav = viewChild.required<IonNav>('flowNav');
  private readonly nav = inject(PublicRedemptionNavService);

  ngAfterViewInit(): void {
    this.nav.setNav(this.flowNav());
  }
}
