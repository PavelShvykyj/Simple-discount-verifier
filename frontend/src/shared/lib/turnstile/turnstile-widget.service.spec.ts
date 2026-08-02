import { HttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { TurnstileWidgetService } from './turnstile-widget.service';

describe('TurnstileWidgetService', () => {
  const render = vi.fn<
    (
      container: HTMLElement,
      options: {
        sitekey: string;
        action: 'start_redemption';
        callback: (token: string) => void;
      },
    ) => string
  >(() => 'widget-id');
  const reset = vi.fn();
  const http = { get: vi.fn() };

  beforeEach(() => {
    render.mockClear();
    reset.mockClear();
    http.get.mockReset();
    window.turnstile = { render, reset, remove: vi.fn() };

    TestBed.configureTestingModule({
      providers: [TurnstileWidgetService, { provide: HttpClient, useValue: http }],
    });
  });

  afterEach(() => {
    delete window.turnstile;
  });

  it('renders the runtime-configured widget and refreshes its one-time token', async () => {
    http.get.mockReturnValue(of({ turnstileEnabled: true, turnstileSiteKey: 'public-site-key' }));
    const service = TestBed.inject(TurnstileWidgetService);
    const host = document.createElement('div');

    await service.render(host);

    expect(service.required()).toBe(true);
    expect(render).toHaveBeenCalledWith(
      host,
      expect.objectContaining({ sitekey: 'public-site-key', action: 'start_redemption' }),
    );

    const options = render.mock.calls[0][1];
    options.callback('one-time-token');
    expect(service.token()).toBe('one-time-token');

    service.refresh();
    expect(service.token()).toBeNull();
    expect(reset).toHaveBeenCalledWith('widget-id');
  });
});
