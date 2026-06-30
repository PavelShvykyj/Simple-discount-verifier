/* eslint-disable @angular-eslint/directive-selector */
import { Directive, DoCheck, ElementRef, Renderer2, inject } from '@angular/core';

@Directive({
  selector: 'ion-button',
  standalone: true,
})
export class DisabledButtonColorDirective implements DoCheck {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private readonly renderer = inject(Renderer2);
  private mediumApplied = false;
  private originalColor: string | null = null;

  ngDoCheck(): void {
    const currentColor = this.host.getAttribute('color');

    if (this.isDisabled()) {
      if (!this.mediumApplied || currentColor !== 'medium') {
        this.originalColor =
          currentColor === 'medium' && this.mediumApplied ? this.originalColor : currentColor;
      }

      this.mediumApplied = true;

      if (currentColor !== 'medium') {
        this.renderer.setAttribute(this.host, 'color', 'medium');
      }

      return;
    }

    if (!this.mediumApplied) {
      return;
    }

    this.mediumApplied = false;

    if (this.originalColor === null) {
      this.renderer.removeAttribute(this.host, 'color');
      return;
    }

    this.renderer.setAttribute(this.host, 'color', this.originalColor);
  }

  private isDisabled(): boolean {
    const disabled = (this.host as HTMLElement & { disabled?: unknown }).disabled;

    return typeof disabled === 'boolean' ? disabled : this.host.hasAttribute('disabled');
  }
}
