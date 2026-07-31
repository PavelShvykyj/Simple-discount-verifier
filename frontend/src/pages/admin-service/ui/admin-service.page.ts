import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  IonCol,
  IonContent,
  IonGrid,
  IonHeader,
  IonButtons,
  IonIcon,
  IonItem,
  IonLabel,
  IonList,
  IonRow,
  IonTitle,
  IonToolbar,
} from '@ionic/angular/standalone';
import { addIcons } from 'ionicons';
import { barcodeOutline, pulseOutline, qrCodeOutline } from 'ionicons/icons';
import { ThemeModeSelectorComponent } from '../../../shared/theme/ui/theme-mode-selector.component';

@Component({
  selector: 'app-admin-service-page',
  imports: [
    IonCol,
    IonContent,
    IonGrid,
    IonHeader,
    IonButtons,
    IonIcon,
    IonItem,
    IonLabel,
    IonList,
    IonRow,
    IonTitle,
    IonToolbar,
    RouterLink,
    ThemeModeSelectorComponent,
  ],
  templateUrl: './admin-service.page.html',
  styleUrl: './admin-service.page.scss',
})
export class AdminServicePage {
  constructor() {
    addIcons({ barcodeOutline, pulseOutline, qrCodeOutline });
  }
}
