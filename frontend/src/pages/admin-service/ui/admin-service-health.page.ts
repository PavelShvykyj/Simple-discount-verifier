import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  IonBackButton,
  IonBadge,
  IonButton,
  IonButtons,
  IonCard,
  IonCardContent,
  IonCardHeader,
  IonCardTitle,
  IonCol,
  IonContent,
  IonGrid,
  IonHeader,
  IonIcon,
  IonItem,
  IonLabel,
  IonList,
  IonRow,
  IonSpinner,
  IonText,
  IonTitle,
  IonToolbar,
} from '@ionic/angular/standalone';
import { addIcons } from 'ionicons';
import { refreshOutline } from 'ionicons/icons';
import { take } from 'rxjs';

import { SystemHealthApi } from '../../../entities/system-health/api/system-health.api';
import { SystemHealthResponse } from '../../../entities/system-health/model/system-health.types';
import { ThemeModeSelectorComponent } from '../../../shared/theme/ui/theme-mode-selector.component';
import { DisabledButtonColorDirective } from '../../../shared/ui/disabled-button-color/disabled-button-color.directive';

type HealthLoadStatus = 'idle' | 'loading' | 'success' | 'error';

@Component({
  selector: 'app-admin-service-health-page',
  imports: [
    DisabledButtonColorDirective,
    IonBackButton,
    IonBadge,
    IonButton,
    IonButtons,
    IonCard,
    IonCardContent,
    IonCardHeader,
    IonCardTitle,
    IonCol,
    IonContent,
    IonGrid,
    IonHeader,
    IonIcon,
    IonItem,
    IonLabel,
    IonList,
    IonRow,
    IonSpinner,
    IonText,
    IonTitle,
    IonToolbar,
    ThemeModeSelectorComponent,
  ],
  templateUrl: './admin-service-health.page.html',
  styleUrl: './admin-service-health.page.scss',
})
export class AdminServiceHealthPage {
  private readonly api = inject(SystemHealthApi);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly status = signal<HealthLoadStatus>('idle');
  protected readonly health = signal<SystemHealthResponse | null>(null);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly isLoading = computed(() => this.status() === 'loading');
  protected readonly statusColor = computed(() =>
    this.health()?.status === 'ok' ? 'success' : 'warning',
  );
  protected readonly serviceStatusText = computed(() =>
    this.health()?.status === 'ok' ? 'Сервіс доступний' : 'Потрібна перевірка',
  );
  protected readonly serviceStatusDescription = computed(() =>
    this.health()?.status === 'ok'
      ? 'Система готова відповідати на запити адміністраторів і каси.'
      : 'Система відповіла, але повідомила про проблему.',
  );

  constructor() {
    addIcons({ refreshOutline });
    this.refresh();
  }

  protected refresh(): void {
    this.status.set('loading');
    this.errorMessage.set(null);

    this.api
      .getHealth()
      .pipe(take(1), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          this.health.set(response);
          this.status.set('success');
        },
        error: (error: unknown) => {
          this.health.set(null);
          this.errorMessage.set(getHealthErrorMessage(error));
          this.status.set('error');
        },
      });
  }
}

function getHealthErrorMessage(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 200) {
      return 'Сервіс відповів у неочікуваному форматі. Повторіть перевірку або зверніться до технічної підтримки.';
    }

    if (error.status > 0) {
      return `Сервіс знижок повернув помилку. Код: ${error.status}.`;
    }
  }

  return 'Не вдалося підключитися до сервісу знижок.';
}
