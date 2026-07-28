import { Component, OnDestroy, computed, inject, signal, viewChild } from '@angular/core';
import {
  IonButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonIcon,
  IonNote,
  IonTitle,
  IonToolbar,
  ModalController,
} from '@ionic/angular/standalone';
import { ZXingScannerComponent, ZXingScannerModule } from '@zxing/ngx-scanner';
import BarcodeFormat from '@zxing/library/esm/core/BarcodeFormat';
import { addIcons } from 'ionicons';
import { closeOutline } from 'ionicons/icons';

import { isValidPhysicalCardNumber } from '../../../../entities/customer-profile/model/physical-card-number';
import { ThemeModeSelectorComponent } from '../../../../shared/theme/ui/theme-mode-selector.component';

@Component({
  selector: 'app-physical-card-scanner',
  imports: [
    IonButton,
    IonButtons,
    IonContent,
    IonHeader,
    IonIcon,
    IonNote,
    IonTitle,
    IonToolbar,
    ThemeModeSelectorComponent,
    ZXingScannerModule,
  ],
  templateUrl: './physical-card-scanner.component.html',
})
export class PhysicalCardScannerComponent implements OnDestroy {
  private readonly modalController = inject(ModalController);
  private readonly scanner = viewChild(ZXingScannerComponent);

  protected readonly hasCamera = signal<boolean | null>(null);
  protected readonly hasPermission = signal<boolean | null>(null);
  protected readonly hasInvalidScan = signal(false);
  protected readonly formats = [BarcodeFormat.EAN_13];
  protected readonly videoConstraints: MediaTrackConstraints = {
    facingMode: { ideal: 'environment' },
  };

  constructor() {
    addIcons({ closeOutline });
  }

  protected readonly statusMessage = computed(() => {
    if (this.hasPermission() === false) {
      return 'Немає доступу до камери. Дозвольте камеру в браузері й повторіть сканування.';
    }

    if (this.hasCamera() === false) {
      return 'Камеру не знайдено на цьому пристрої.';
    }

    if (this.hasInvalidScan()) {
      return 'Розпізнано невалідний EAN-13. Спробуйте ще раз.';
    }

    return 'Наведіть камеру на штрихкод фізичної картки.';
  });

  protected readonly statusColor = computed(() =>
    this.hasPermission() === false || this.hasCamera() === false || this.hasInvalidScan()
      ? 'danger'
      : 'medium',
  );

  protected close(): void {
    void this.modalController.dismiss();
  }

  protected handleScanSuccess(value: string): void {
    if (!isValidPhysicalCardNumber(value)) {
      this.hasInvalidScan.set(true);
      return;
    }

    void this.modalController.dismiss({ value });
  }

  protected handlePermissionResponse(hasPermission: boolean): void {
    this.hasPermission.set(hasPermission);
  }

  protected handleCamerasFound(cameras: MediaDeviceInfo[]): void {
    this.hasCamera.set(cameras.length > 0);
  }

  protected handleCamerasNotFound(): void {
    this.hasCamera.set(false);
  }

  ngOnDestroy(): void {
    this.scanner()?.reset();
  }
}
