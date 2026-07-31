import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  computed,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { IonNote } from '@ionic/angular/standalone';
import { ZXingScannerComponent, ZXingScannerModule } from '@zxing/ngx-scanner';

@Component({
  selector: 'app-support-qr-scanner',
  imports: [IonNote, ZXingScannerModule],
  templateUrl: './support-qr-scanner.component.html',
  styleUrl: './support-qr-scanner.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SupportQrScannerComponent implements OnDestroy {
  readonly scanned = output<string>();

  private readonly scanner = viewChild(ZXingScannerComponent);

  protected readonly lastScannedValue = signal<string | null>(null);
  protected readonly hasCamera = signal<boolean | null>(null);
  protected readonly hasPermission = signal<boolean | null>(null);

  protected readonly statusMessage = computed(() => {
    if (this.hasPermission() === false) {
      return 'Немає доступу до камери. Дозвольте камеру в браузері й повторіть тест.';
    }

    if (this.hasCamera() === false) {
      return 'Камеру не знайдено на цьому пристрої.';
    }

    const scannedValue = this.lastScannedValue();

    if (scannedValue === null) {
      return 'Наведіть камеру на QR-код, відкритий на іншому телефоні.';
    }

    return `Відскановано: ${scannedValue}`;
  });

  protected handleScanSuccess(value: string): void {
    this.lastScannedValue.set(value);
    this.scanned.emit(value);
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
