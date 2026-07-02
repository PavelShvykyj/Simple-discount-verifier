/* eslint-disable @angular-eslint/component-selector, @angular-eslint/directive-selector */
import { ComponentFixture, TestBed } from '@angular/core/testing';

describe('AdminShellComponent', () => {
  let fixture: ComponentFixture<unknown>;
  let AdminShellComponent: new () => unknown;

  beforeEach(async () => {
    vi.resetModules();

    vi.doMock('@ionic/angular/standalone', async () => {
      const { Component, Directive, input } =
        await vi.importActual<typeof import('@angular/core')>('@angular/core');

      @Component({
        selector: 'ion-tabs',
        standalone: true,
        template: '<ng-content />',
      })
      class IonTabs {}

      @Component({
        selector: 'ion-tab-bar',
        standalone: true,
        template: '<ng-content />',
      })
      class IonTabBar {}

      @Component({
        selector: 'ion-tab-button',
        standalone: true,
        template: '<ng-content />',
      })
      class IonTabButton {
        readonly tab = input<string>();
        readonly href = input<string>();
      }

      @Component({
        selector: 'ion-icon',
        standalone: true,
        template: '',
      })
      class IonIcon {
        readonly name = input<string>();
      }

      @Component({
        selector: 'ion-label',
        standalone: true,
        template: '<ng-content />',
      })
      class IonLabel {}

      @Directive({
        selector: '[slot]',
        standalone: true,
      })
      class SlotDirective {
        readonly slot = input<string>();
      }

      return { IonIcon, IonLabel, IonTabBar, IonTabButton, IonTabs, SlotDirective };
    });

    ({ AdminShellComponent } = await import('./admin-shell.component'));

    TestBed.configureTestingModule({
      imports: [AdminShellComponent],
    });

    fixture = TestBed.createComponent(AdminShellComponent);
    fixture.detectChanges();
  });

  afterEach(() => {
    vi.doUnmock('@ionic/angular/standalone');
  });

  it('shows create profile as the first admin tab', () => {
    const tabButtons = Array.from(
      fixture.nativeElement.querySelectorAll('ion-tab-button'),
    ) as HTMLElement[];

    expect(tabButtons).toHaveLength(3);
    expect(tabButtons[0].getAttribute('tab')).toBe('customer-create');
    expect(tabButtons[0].getAttribute('href')).toBe('/admin/customer-create');
    expect(tabButtons[0].textContent).toContain('Створити анкету');
    expect(tabButtons[1].getAttribute('tab')).toBe('customers');
    expect(tabButtons[1].textContent).toContain('Анкети');
  });
});
