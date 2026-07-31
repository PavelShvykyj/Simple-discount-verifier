import { Component, effect, input, signal } from '@angular/core';
import { IonNote, IonSpinner, IonText } from '@ionic/angular/standalone';

import { BarcodeResult } from '../../model/redemption-flow.types';

@Component({
  selector: 'app-barcode-renderer',
  imports: [IonNote, IonSpinner, IonText],
  templateUrl: './barcode-renderer.component.html',
  styleUrl: './barcode-renderer.component.scss',
})
export class BarcodeRendererComponent {
  readonly value = input.required<string>();
  readonly format = input.required<BarcodeResult['barcodeFormat']>();

  protected readonly barcodeImage = signal<string | null>(null);
  protected readonly isRendering = signal(false);
  protected readonly hasRenderError = signal(false);

  private renderRequestId = 0;

  constructor() {
    effect(() => {
      const value = this.value();
      const format = this.format();

      void this.renderBarcode(value, format);
    });
  }

  private async renderBarcode(
    value: string,
    format: BarcodeResult['barcodeFormat'],
  ): Promise<void> {
    const requestId = ++this.renderRequestId;

    this.isRendering.set(true);
    this.hasRenderError.set(false);
    this.barcodeImage.set(null);

    try {
      const bwipjs = await import('@bwip-js/browser');
      const svg = bwipjs.toSVG({
        bcid: format,
        text: value,
        scale: 3,
        height: 18,
        includetext: false,
        paddingwidth: 8,
        paddingheight: 8,
        backgroundcolor: 'FFFFFF',
      });

      if (requestId === this.renderRequestId) {
        this.barcodeImage.set(svgToDataUrl(svg));
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
