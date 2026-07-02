import { Component, DestroyRef, computed, effect, inject, input, output, signal } from '@angular/core';

const COUNTDOWN_TICK_MS = 1000;

@Component({
  selector: 'app-countdown-timer',
  templateUrl: './countdown-timer.component.html',
  styleUrl: './countdown-timer.component.scss',
})
export class CountdownTimerComponent {
  readonly targetAt = input.required<string | number | Date>();
  readonly completedText = input('0:00');
  readonly remainingSecondsChange = output<number>();

  private readonly destroyRef = inject(DestroyRef);
  private readonly now = signal(Date.now());
  private lastEmittedRemainingSeconds: number | null = null;

  protected readonly remainingSeconds = computed(() => {
    const remainingMs = toTimestamp(this.targetAt()) - this.now();

    return Math.max(0, Math.ceil(remainingMs / COUNTDOWN_TICK_MS));
  });

  protected readonly displayText = computed(() => {
    const remainingSeconds = this.remainingSeconds();

    if (remainingSeconds === 0) {
      return this.completedText();
    }

    const minutes = Math.floor(remainingSeconds / 60);
    const seconds = String(remainingSeconds % 60).padStart(2, '0');

    return `${minutes}:${seconds}`;
  });

  constructor() {
    const tick = window.setInterval(() => this.now.set(Date.now()), COUNTDOWN_TICK_MS);
    this.destroyRef.onDestroy(() => window.clearInterval(tick));

    effect(() => {
      const remainingSeconds = this.remainingSeconds();

      if (remainingSeconds !== this.lastEmittedRemainingSeconds) {
        this.lastEmittedRemainingSeconds = remainingSeconds;
        this.remainingSecondsChange.emit(remainingSeconds);
      }
    });
  }
}

function toTimestamp(value: string | number | Date): number {
  if (value instanceof Date) {
    return value.getTime();
  }

  if (typeof value === 'number') {
    return value;
  }

  const timestamp = Date.parse(value);

  return Number.isNaN(timestamp) ? Date.now() : timestamp;
}
