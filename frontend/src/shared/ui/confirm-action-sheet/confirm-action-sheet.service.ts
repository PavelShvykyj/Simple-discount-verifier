import { Injectable, inject } from '@angular/core';
import {
  ActionSheetButton,
  ActionSheetController,
  ActionSheetOptions,
} from '@ionic/angular/standalone';

export type ConfirmImportance = 'info' | 'warning' | 'critical';

export interface ConfirmActionSheetOptions {
  readonly header: string;
  readonly message?: string;
  readonly confirmText: string;
  readonly cancelText?: string;
  readonly importance: ConfirmImportance;
}

const CONFIRM_ROLE = 'confirm';
const CANCEL_ROLE = 'cancel';
const IMPORTANCE_SHEET_CLASS: Record<ConfirmImportance, string> = {
  info: 'sdv-confirm-action-sheet--info',
  warning: 'sdv-confirm-action-sheet--warning',
  critical: 'sdv-confirm-action-sheet--critical',
};

@Injectable({ providedIn: 'root' })
export class ConfirmActionSheetService {
  private readonly actionSheetController = inject(ActionSheetController);

  async confirm(options: ConfirmActionSheetOptions): Promise<boolean> {
    const actionSheet = await this.actionSheetController.create({
      header: options.header,
      subHeader: options.message,
      cssClass: ['sdv-confirm-action-sheet', IMPORTANCE_SHEET_CLASS[options.importance]],
      buttons: this.createButtons(options),
    } satisfies ActionSheetOptions);

    await actionSheet.present();
    const result = await actionSheet.onDidDismiss();

    return result.role === CONFIRM_ROLE;
  }

  private createButtons(options: ConfirmActionSheetOptions): ActionSheetButton[] {
    return [
      {
        text: options.confirmText,
        role: CONFIRM_ROLE,
      },
      {
        text: options.cancelText ?? 'Скасувати',
        role: CANCEL_ROLE,
      },
    ];
  }
}
