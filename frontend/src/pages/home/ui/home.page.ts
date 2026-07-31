import { Component } from '@angular/core';
import {
  IonButtons,
  IonCol,
  IonContent,
  IonGrid,
  IonHeader,
  IonRow,
  IonTitle,
  IonToolbar,
} from '@ionic/angular/standalone';

import { ThemeModeSelectorComponent } from '../../../shared/theme/ui/theme-mode-selector.component';

@Component({
  selector: 'app-home-page',
  imports: [
    IonButtons,
    IonCol,
    IonContent,
    IonGrid,
    IonHeader,
    IonRow,
    IonTitle,
    IonToolbar,
    ThemeModeSelectorComponent,
  ],
  templateUrl: './home.page.html',
  styleUrl: './home.page.scss',
})
export class HomePage {}
