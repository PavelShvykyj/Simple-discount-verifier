import { DatePipe } from '@angular/common';
import { Component, DestroyRef, computed, effect, inject, signal } from '@angular/core';
import { AbstractControl, ValidationErrors } from '@angular/forms';
import {
  IonButton,
  IonButtons,
  IonCol,
  IonContent,
  IonFab,
  IonFabButton,
  IonGrid,
  IonHeader,
  IonIcon,
  IonItem,
  IonItemOption,
  IonItemOptions,
  IonItemSliding,
  IonLabel,
  IonList,
  ModalController,
  IonRefresher,
  IonRefresherContent,
  IonRow,
  IonSpinner,
  IonText,
  IonTitle,
  IonToolbar,
} from '@ionic/angular/standalone';
import { addIcons } from 'ionicons';
import { addOutline, documentTextOutline, searchOutline } from 'ionicons/icons';

import { AdminCustomerProfilesApi } from '../../../entities/customer-profile/api/admin-customer-profiles.api';
import { CUSTOMER_PROFILE_FORM_CONFIG } from '../../../entities/customer-profile/model/customer-profile-form.config';
import { toCustomerProfileListItem } from '../../../entities/customer-profile/model/customer-profile-list.mapper';
import {
  CustomerProfile,
  CustomerProfileListItem,
} from '../../../entities/customer-profile/model/customer-profile.types';
import { CustomerProfileFormComponent } from '../../../features/customer-profile-form/ui/customer-profile-form.component';
import {
  CustomerProfileListQuery,
  createCustomerProfileListDataSource,
} from '../../../features/customer-profile-list/model/customer-profile-list.datasource';
import {
  INVALID_UKRAINIAN_PHONE_MESSAGE,
  normalizeUkrainianPhone,
} from '../../../shared/lib/phone/ukrainian-phone';
import {
  SubmitSearchFieldComponent,
  SubmitSearchFieldValidator,
} from '../../../shared/ui/submit-search-field/submit-search-field.component';
import { ThemeModeSelectorComponent } from '../../../shared/theme/ui/theme-mode-selector.component';
import { AppToastService } from '../../../shared/ui/toast/app-toast.service';

interface AdminCustomerProfileListItem extends CustomerProfileListItem {
  readonly profile: CustomerProfile;
}

@Component({
  selector: 'app-admin-customers-page',
  imports: [
    DatePipe,
    IonButton,
    IonButtons,
    IonCol,
    IonContent,
    IonFab,
    IonFabButton,
    IonGrid,
    IonHeader,
    IonIcon,
    IonItem,
    IonItemOption,
    IonItemOptions,
    IonItemSliding,
    IonLabel,
    IonList,
    IonRefresher,
    IonRefresherContent,
    IonRow,
    IonSpinner,
    IonText,
    IonTitle,
    IonToolbar,
    SubmitSearchFieldComponent,
    ThemeModeSelectorComponent,
  ],
  templateUrl: './admin-customers.page.html',
  styleUrl: './admin-customers.page.scss',
})
export class AdminCustomersPage {
  private readonly api = inject(AdminCustomerProfilesApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly modalController = inject(ModalController);
  private readonly toast = inject(AppToastService);
  private readonly dataSource = createCustomerProfileListDataSource(this.api);
  private activeRefresher: HTMLIonRefresherElement | null = null;
  private lastShownErrorMessage: string | null = null;

  protected readonly phoneConfig = CUSTOMER_PROFILE_FORM_CONFIG.phone;
  protected readonly phoneSearchValidators: readonly SubmitSearchFieldValidator[] = [
    {
      errorKey: 'ukrainianPhone',
      message: INVALID_UKRAINIAN_PHONE_MESSAGE,
      validator: ukrainianPhoneValidator,
    },
  ];
  protected readonly listView = this.dataSource.connect();
  protected readonly isSearchVisible = signal(false);
  protected readonly profileItems = computed<readonly AdminCustomerProfileListItem[]>(() =>
    this.listView().items.map((profile) => ({
      ...toCustomerProfileListItem(profile),
      profile,
    })),
  );
  protected readonly isLoading = computed(() => this.listView().status === 'loading');
  protected readonly isLoadingMore = computed(() => this.listView().status === 'loadingMore');
  protected readonly hasProfiles = computed(() => this.listView().items.length > 0);
  protected readonly canLoadMore = computed(() => {
    const view = this.listView();

    return (
      view.continuationToken !== null &&
      view.query?.kind === 'all' &&
      view.status !== 'loading' &&
      view.status !== 'loadingMore'
    );
  });
  protected readonly isSearchBusy = computed(() => {
    const view = this.listView();

    return view.status === 'loading' && view.query?.kind === 'phone';
  });

  constructor() {
    addIcons({ addOutline, documentTextOutline, searchOutline });
    this.loadAllProfiles();
    this.destroyRef.onDestroy(() => this.dataSource.disconnect());

    effect(() => {
      const status = this.listView().status;

      if (
        this.activeRefresher === null ||
        status === 'loading' ||
        status === 'loadingMore'
      ) {
        return;
      }

      void this.activeRefresher.complete();
      this.activeRefresher = null;
    });

    effect(() => {
      const view = this.listView();

      if (view.status === 'loading' || view.status === 'loadingMore') {
        this.lastShownErrorMessage = null;
        return;
      }

      const errorMessage = view.error?.message ?? null;

      if (errorMessage === null || errorMessage === this.lastShownErrorMessage) {
        return;
      }

      this.lastShownErrorMessage = errorMessage;
      void this.toast.showError(errorMessage);
    });
  }

  protected toggleSearch(): void {
    this.isSearchVisible.update((isVisible) => !isVisible);
  }

  protected searchByPhone(phone: string): void {
    const query = this.toPhoneQuery(phone);
    if (query === null) {
      return;
    }

    this.dataSource.load(query);
  }

  protected loadAllProfiles(): void {
    this.dataSource.load({ kind: 'all' });
  }

  protected loadMore(): void {
    this.dataSource.loadMore();
  }

  protected refreshProfiles(event: Event): void {
    this.activeRefresher = event.target as HTMLIonRefresherElement;
    this.loadAllProfiles();
  }

  protected async openCreateProfileModal(): Promise<void> {
    await this.openProfileModal({ mode: 'create', profile: null });
  }

  protected async openEditProfileModal(profile: CustomerProfile): Promise<void> {
    await this.openProfileModal({ mode: 'edit', profile });
  }

  private toPhoneQuery(phone: string): CustomerProfileListQuery | null {
    const normalizedPhone = normalizeUkrainianPhone(phone);

    return normalizedPhone === null
      ? null
      : {
          kind: 'phone',
          phone: normalizedPhone,
        };
  }

  private async openProfileModal(options: {
    readonly mode: 'create' | 'edit';
    readonly profile: CustomerProfile | null;
  }): Promise<void> {
    let canDismiss = async (): Promise<boolean> => true;
    const modal = await this.modalController.create({
      component: CustomerProfileFormComponent,
      componentProps: {
        ...options,
        presentation: 'modal',
        registerCanDismiss: (handler: () => Promise<boolean>) => {
          canDismiss = handler;
        },
      },
      canDismiss: async (data?: { saved?: boolean }) =>
        data?.saved === true ? true : await canDismiss(),
    });

    await modal.present();

    const result = await modal.onDidDismiss<{ saved?: boolean }>();

    if (result.data?.saved) {
      this.loadAllProfiles();
    }
  }
}

function ukrainianPhoneValidator(control: AbstractControl): ValidationErrors | null {
  const value = String(control.value ?? '');

  if (value.trim().length === 0) {
    return null;
  }

  return normalizeUkrainianPhone(value) === null ? { ukrainianPhone: true } : null;
}
