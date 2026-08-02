import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

const SCRIPT_URL = 'https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit';
const START_REDEMPTION_ACTION = 'start_redemption';

interface PublicRedemptionConfig {
  readonly turnstileEnabled: boolean;
  readonly turnstileSiteKey: string | null;
}

interface TurnstileRenderOptions {
  readonly sitekey: string;
  readonly action: typeof START_REDEMPTION_ACTION;
  readonly callback: (token: string) => void;
  readonly 'error-callback'?: () => void;
  readonly 'expired-callback'?: () => void;
}

interface TurnstileApi {
  render(container: HTMLElement, options: TurnstileRenderOptions): string;
  reset(widgetId: string): void;
  remove(widgetId: string): void;
}

declare global {
  interface Window {
    turnstile?: TurnstileApi;
  }
}

let scriptLoadPromise: Promise<void> | null = null;

function loadScript(): Promise<void> {
  if (typeof window === 'undefined') {
    return Promise.reject(new Error('Turnstile is only available in a browser context.'));
  }

  if (window.turnstile) {
    return Promise.resolve();
  }

  scriptLoadPromise ??= new Promise<void>((resolve, reject) => {
    const script = document.createElement('script');
    script.src = SCRIPT_URL;
    script.async = true;
    script.defer = true;
    script.addEventListener('load', () => resolve());
    script.addEventListener('error', () => {
      scriptLoadPromise = null;
      reject(new Error('Failed to load Cloudflare Turnstile script.'));
    });
    document.head.appendChild(script);
  });

  return scriptLoadPromise;
}

/**
 * Invisible Cloudflare Turnstile widget wrapper.
 *
 * The public site key and enabled flag come from the backend runtime configuration, so enabling
 * Turnstile does not require rebuilding the static frontend. The backend remains authoritative.
 */
@Injectable()
export class TurnstileWidgetService {
  readonly token = signal<string | null>(null);
  readonly required = signal(true);

  private readonly http = inject(HttpClient);
  private widgetId: string | null = null;

  async render(container: HTMLElement): Promise<void> {
    let config: PublicRedemptionConfig;

    try {
      config = await firstValueFrom(
        this.http.get<PublicRedemptionConfig>('/api/public/redemptions/config'),
      );
    } catch {
      return;
    }

    this.required.set(config.turnstileEnabled);
    const siteKey = config.turnstileEnabled ? config.turnstileSiteKey : null;

    if (!siteKey) {
      return;
    }

    try {
      await loadScript();
    } catch {
      this.token.set(null);
      return;
    }

    try {
      this.widgetId = window.turnstile!.render(container, {
        sitekey: siteKey,
        action: START_REDEMPTION_ACTION,
        callback: (token) => this.token.set(token),
        'error-callback': () => this.token.set(null),
        'expired-callback': () => this.refresh(),
      });
    } catch {
      this.token.set(null);
    }
  }

  /** Requests a fresh token (e.g. after a token has been consumed by a submit attempt). */
  refresh(): void {
    this.token.set(null);

    if (this.widgetId !== null && window.turnstile) {
      window.turnstile.reset(this.widgetId);
    }
  }
}
