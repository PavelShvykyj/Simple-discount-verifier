import { Component } from '@angular/core';
import {
  IonIcon,
  IonLabel,
  IonTabBar,
  IonTabButton,
  IonTabs,
} from '@ionic/angular/standalone';
import { addIcons } from 'ionicons';
import { constructOutline, peopleOutline } from 'ionicons/icons';

@Component({
  selector: 'app-admin-shell',
  imports: [IonIcon, IonLabel, IonTabBar, IonTabButton, IonTabs],
  templateUrl: './admin-shell.component.html',
  styleUrl: './admin-shell.component.scss',
})
export class AdminShellComponent {
  constructor() {
    addIcons({ constructOutline, peopleOutline });
  }
}
