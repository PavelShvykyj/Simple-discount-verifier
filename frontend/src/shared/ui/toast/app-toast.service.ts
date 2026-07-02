import { Injectable, inject } from '@angular/core';
import { ToastController } from '@ionic/angular/standalone';

type AppToastKind = 'success' | 'error' | 'warning' | 'info';

const TOAST_COLORS: Record<AppToastKind, string> = {
  success: 'success',
  error: 'danger',
  warning: 'warning',
  info: 'primary',
};

@Injectable({ providedIn: 'root' })
export class AppToastService {
  private readonly toastController = inject(ToastController);

  async showSuccess(message: string): Promise<void> {
    await this.show('success', message);
  }

  async showError(message: string): Promise<void> {
    await this.show('error', message);
  }

  async showWarning(message: string): Promise<void> {
    await this.show('warning', message);
  }

  async showInfo(message: string): Promise<void> {
    await this.show('info', message);
  }

  private async show(kind: AppToastKind, message: string): Promise<void> {
    const toast = await this.toastController.create({
      message,
      color: TOAST_COLORS[kind],
      duration: 8000,
      position: 'bottom',
      buttons: [
        {
          text: 'Закрити',
          role: 'cancel',
        },
      ],
    });

    await toast.present();
  }
}
