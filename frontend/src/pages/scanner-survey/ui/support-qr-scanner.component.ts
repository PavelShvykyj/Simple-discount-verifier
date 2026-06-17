import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  computed,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { IonNote } from '@ionic/angular/standalone';
import { ZXingScannerComponent, ZXingScannerModule } from '@zxing/ngx-scanner';

@Component({
  selector: 'app-support-qr-scanner',
  imports: [IonNote, ZXingScannerModule],
  template: `
    <div class="scanner-frame">
      <zxing-scanner
        #scanner
        [tryHarder]="true"
        (scanSuccess)="handleScanSuccess($event)"
        (permissionResponse)="handlePermissionResponse($event)"
        (camerasFound)="handleCamerasFound($event)"
        (camerasNotFound)="handleCamerasNotFound()"
      />
    </div>

    @if (statusMessage(); as message) {
      <ion-note [color]="isMatched() ? 'success' : 'medium'">{{ message }}</ion-note>
    }
  `,
  styles: [
    `
      :host {
        display: grid;
        gap: 0.75rem;
      }

      .scanner-frame {
        overflow: hidden;
        border: 1px solid var(--app-border);
        border-radius: 8px;
        background: #000000;
      }

      zxing-scanner {
        display: block;
        min-height: 16rem;
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SupportQrScannerComponent implements OnDestroy {
  readonly expectedPrefix = input.required<string>();
  readonly matched = output<string>();

  private readonly scanner = viewChild(ZXingScannerComponent);

  protected readonly lastScannedValue = signal<string | null>(null);
  protected readonly hasCamera = signal<boolean | null>(null);
  protected readonly hasPermission = signal<boolean | null>(null);

  protected readonly isMatched = computed(() => this.isSupportQrValue(this.lastScannedValue()));
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

    if (this.isMatched()) {
      return 'QR-код прочитано й підтверджено.';
    }

    return 'Прочитано інший QR-код. Наведіть камеру на QR цього тесту.';
  });

  protected handleScanSuccess(value: string): void {
    this.lastScannedValue.set(value);

    if (this.isSupportQrValue(value)) {
      this.matched.emit(value);
    }
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

  private isSupportQrValue(value: string | null): boolean {
    if (value === null || !value.startsWith(this.expectedPrefix())) {
      return false;
    }

    return /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(
      value.slice(this.expectedPrefix().length),
    );
  }
}
