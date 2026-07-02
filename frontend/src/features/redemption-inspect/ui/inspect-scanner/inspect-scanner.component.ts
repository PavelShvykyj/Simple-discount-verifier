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
import { addIcons } from 'ionicons';
import { closeOutline } from 'ionicons/icons';
import { ThemeModeSelectorComponent } from '../../../../shared/theme/ui/theme-mode-selector.component';

@Component({
  selector: 'app-inspect-scanner',
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
  templateUrl: './inspect-scanner.component.html',
  styleUrl: './inspect-scanner.component.scss',
})
export class InspectScannerComponent implements OnDestroy {
  private readonly modalController = inject(ModalController);
  private readonly scanner = viewChild(ZXingScannerComponent);

  protected readonly lastScannedValue = signal<string | null>(null);
  protected readonly hasCamera = signal<boolean | null>(null);
  protected readonly hasPermission = signal<boolean | null>(null);

  constructor() {
    addIcons({ closeOutline });
  }

  protected readonly statusMessage = computed(() => {
    if (this.hasPermission() === false) {
      return 'Немає доступу до камери. Дозвольте камеру в налаштуваннях пристрою й повторіть сканування.';
    }

    if (this.hasCamera() === false) {
      return 'Камеру не знайдено на цьому пристрої.';
    }

    return this.lastScannedValue() ?? 'Наведіть камеру на QR-код або штрихкод клієнта.';
  });

  protected close(): void {
    void this.modalController.dismiss();
  }

  protected handleScanSuccess(value: string): void {
    this.lastScannedValue.set(value);
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
