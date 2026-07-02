import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CountdownTimerComponent } from './countdown-timer.component';

describe('CountdownTimerComponent', () => {
  let fixture: ComponentFixture<CountdownTimerComponent>;

  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-06-30T12:00:00Z'));

    TestBed.configureTestingModule({
      imports: [CountdownTimerComponent],
    });
    fixture = TestBed.createComponent(CountdownTimerComponent);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('renders the remaining time as minutes and padded seconds', () => {
    fixture.componentRef.setInput('targetAt', Date.now() + 65_000);
    fixture.detectChanges();

    expect(textContent()).toBe('1:05');
  });

  it('updates every second and renders the completed text at zero', () => {
    fixture.componentRef.setInput('targetAt', Date.now() + 1_000);
    fixture.componentRef.setInput('completedText', 'готово');
    fixture.detectChanges();

    expect(textContent()).toBe('0:01');

    vi.advanceTimersByTime(1_000);
    fixture.detectChanges();

    expect(textContent()).toBe('готово');
  });

  function textContent(): string {
    return (fixture.nativeElement.textContent as string).trim();
  }
});
