import { Component } from '@angular/core';
import {
  IonButton,
  IonContent,
  IonHeader,
  IonIcon,
  IonItem,
  IonLabel,
  IonList,
  IonText,
  IonTitle,
  IonToolbar,
} from '@ionic/angular/standalone';
import { addIcons } from 'ionicons';
import { barcodeOutline, chatbubbleEllipsesOutline, phonePortraitOutline } from 'ionicons/icons';

@Component({
  selector: 'app-home-page',
  imports: [
    IonButton,
    IonContent,
    IonHeader,
    IonIcon,
    IonItem,
    IonLabel,
    IonList,
    IonText,
    IonTitle,
    IonToolbar,
  ],
  templateUrl: './home.page.html',
  styleUrl: './home.page.scss',
})
export class HomePage {
  constructor() {
    addIcons({
      barcodeOutline,
      chatbubbleEllipsesOutline,
      phonePortraitOutline,
    });
  }
}
