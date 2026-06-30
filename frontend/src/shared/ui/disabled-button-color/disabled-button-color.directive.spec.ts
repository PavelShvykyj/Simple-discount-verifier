import { Component, CUSTOM_ELEMENTS_SCHEMA, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DisabledButtonColorDirective } from './disabled-button-color.directive';

@Component({
  imports: [DisabledButtonColorDirective],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  template: `
    <ion-button
      color="secondary"
      [disabled]="isDisabled()"
      [appDisabledButtonColor]="isDisabled()"
    >
      Action
    </ion-button>
  `,
})
class HostWithColorComponent {
  readonly isDisabled = signal(false);
}

@Component({
  imports: [DisabledButtonColorDirective],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  template: `
    <ion-button [disabled]="isDisabled()" [appDisabledButtonColor]="isDisabled()">
      Action
    </ion-button>
  `,
})
class HostWithoutColorComponent {
  readonly isDisabled = signal(true);
}

describe('DisabledButtonColorDirective', () => {
  it('sets medium color while disabled and restores the initial color', () => {
    const fixture = TestBed.createComponent(HostWithColorComponent);
    fixture.detectChanges();
    const button = getButton(fixture);

    expect(button.getAttribute('color')).toBe('secondary');

    fixture.componentInstance.isDisabled.set(true);
    fixture.detectChanges();
    expect(button.getAttribute('color')).toBe('medium');

    fixture.componentInstance.isDisabled.set(false);
    fixture.detectChanges();
    expect(button.getAttribute('color')).toBe('secondary');
  });

  it('removes the color attribute when re-enabled if the button had no initial color', () => {
    const fixture = TestBed.createComponent(HostWithoutColorComponent);
    fixture.detectChanges();
    const button = getButton(fixture);

    expect(button.getAttribute('color')).toBe('medium');

    fixture.componentInstance.isDisabled.set(false);
    fixture.detectChanges();
    expect(button.hasAttribute('color')).toBe(false);
  });
});

function getButton(fixture: ComponentFixture<unknown>): HTMLElement {
  return fixture.nativeElement.querySelector('ion-button') as HTMLElement;
}
