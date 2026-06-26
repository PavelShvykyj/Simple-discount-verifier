import { Component } from '@angular/core';
import {
  IonIcon,
  IonLabel,
  IonRouterOutlet,
  IonTabBar,
  IonTabButton,
  IonTabs,
} from '@ionic/angular/standalone';
import { addIcons } from 'ionicons';
import { constructOutline, peopleOutline } from 'ionicons/icons';

import { ThemeModeSelectorComponent } from '../../../shared/theme/ui/theme-mode-selector.component';

@Component({
  selector: 'app-admin-shell',
  imports: [
    IonIcon,
    IonLabel,
    IonRouterOutlet,
    IonTabBar,
    IonTabButton,
    IonTabs,
    ThemeModeSelectorComponent,
  ],
  templateUrl: './admin-shell.component.html',
  styleUrl: './admin-shell.component.scss',
})
export class AdminShellComponent {
  constructor() {
    addIcons({ constructOutline, peopleOutline });
  }
}
