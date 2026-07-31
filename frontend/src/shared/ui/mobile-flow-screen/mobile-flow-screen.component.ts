import { Component } from '@angular/core';
import { IonCol, IonContent, IonFooter, IonGrid, IonRow, IonToolbar } from '@ionic/angular/standalone';

@Component({
  selector: 'app-mobile-flow-screen',
  imports: [IonCol, IonContent, IonFooter, IonGrid, IonRow, IonToolbar],
  host: {
    class: 'ion-page',
  },
  templateUrl: './mobile-flow-screen.component.html',
  styleUrl: './mobile-flow-screen.component.scss',
})
export class MobileFlowScreenComponent {}
