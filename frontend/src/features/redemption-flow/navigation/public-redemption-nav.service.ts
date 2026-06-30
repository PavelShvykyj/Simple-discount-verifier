import { Injectable, Type, signal } from '@angular/core';
import { IonNav } from '@ionic/angular/standalone';

@Injectable()
export class PublicRedemptionNavService {
  private readonly canGoBackState = signal(false);

  readonly canGoBack = this.canGoBackState.asReadonly();

  private nav: IonNav | null = null;

  setNav(nav: IonNav): void {
    this.nav = nav;
    void this.updateCanGoBack();
  }

  async pushSms(): Promise<void> {
    const { SmsVerificationScreenComponent } = await import(
      '../ui/sms-verification-screen/sms-verification-screen.component'
    );

    await this.push(SmsVerificationScreenComponent);
  }

  async pushBarcode(): Promise<void> {
    const { BarcodeResultScreenComponent } = await import(
      '../ui/barcode-result-screen/barcode-result-screen.component'
    );

    await this.push(BarcodeResultScreenComponent);
  }

  async resetToPhone(): Promise<void> {
    const { PhoneEntryScreenComponent } = await import(
      '../ui/phone-entry-screen/phone-entry-screen.component'
    );

    await this.nav?.setRoot(PhoneEntryScreenComponent);
    await this.updateCanGoBack();
  }

  async goBack(): Promise<void> {
    const canGoBack = (await this.nav?.canGoBack()) ?? false;

    if (canGoBack) {
      await this.nav?.pop();
    }

    await this.updateCanGoBack();
  }

  private async push(component: Type<unknown>): Promise<void> {
    await this.nav?.push(component);
    await this.updateCanGoBack();
  }

  private async updateCanGoBack(): Promise<void> {
    this.canGoBackState.set((await this.nav?.canGoBack()) ?? false);
  }
}
