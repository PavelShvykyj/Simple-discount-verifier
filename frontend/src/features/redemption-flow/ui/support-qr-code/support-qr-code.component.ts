import { Component, computed, effect, input, signal } from '@angular/core';
import { IonNote, IonSpinner, IonText } from '@ionic/angular/standalone';

import { formatSupportQrPayload } from '../../model/support-qr-payload';

@Component({
  selector: 'app-support-qr-code',
  imports: [IonNote, IonSpinner, IonText],
  templateUrl: './support-qr-code.component.html',
  styleUrl: './support-qr-code.component.scss',
})
export class SupportQrCodeComponent {
  readonly correlationId = input.required<string>();

  protected readonly supportPayload = computed(() => formatSupportQrPayload(this.correlationId()));
  protected readonly qrImage = signal<string | null>(null);
  protected readonly isRendering = signal(false);
  protected readonly hasRenderError = signal(false);

  private renderRequestId = 0;

  constructor() {
    effect(() => {
      const payload = this.supportPayload();

      void this.renderQr(payload);
    });
  }

  private async renderQr(payload: string): Promise<void> {
    const requestId = ++this.renderRequestId;

    this.isRendering.set(true);
    this.hasRenderError.set(false);
    this.qrImage.set(null);

    try {
      const bwipjs = await import('@bwip-js/browser');
      const svg = bwipjs.toSVG({
        bcid: 'qrcode',
        text: payload,
        scale: 6,
        paddingwidth: 8,
        paddingheight: 8,
        backgroundcolor: 'FFFFFF',
      });

      if (requestId === this.renderRequestId) {
        this.qrImage.set(svgToDataUrl(svg));
      }
    } catch {
      if (requestId === this.renderRequestId) {
        this.hasRenderError.set(true);
      }
    } finally {
      if (requestId === this.renderRequestId) {
        this.isRendering.set(false);
      }
    }
  }
}

function svgToDataUrl(svg: string): string {
  return `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`;
}
