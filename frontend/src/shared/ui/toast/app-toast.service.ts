import { Injectable, inject } from '@angular/core';
import { ToastController } from '@ionic/angular/standalone';

import type { ApiErrorMessage } from '../../lib/api-error/api-error';
import { toApiErrorDisplayMessage } from '../../lib/api-error/api-error';

type AppToastKind = 'success' | 'error' | 'warning' | 'info';

const TOAST_COLORS: Record<AppToastKind, string> = {
  success: 'success',
  error: 'danger',
  warning: 'warning',
  info: 'primary',
};
const DEFAULT_ERROR_MESSAGE = 'Сталася помилка.';

@Injectable({ providedIn: 'root' })
export class AppToastService {
  private readonly toastController = inject(ToastController);

  async showSuccess(message: string): Promise<void> {
    await this.show('success', message);
  }

  async showError(
    message: ApiErrorMessage,
    fallbackMessage = DEFAULT_ERROR_MESSAGE,
  ): Promise<void> {
    await this.show('error', toApiErrorDisplayMessage(message, fallbackMessage));
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
