import { Directive, ElementRef, Renderer2, booleanAttribute, effect, inject, input } from '@angular/core';

@Directive({
  selector: '[appDisabledButtonColor]',
  standalone: true,
})
export class DisabledButtonColorDirective {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private readonly renderer = inject(Renderer2);
  private readonly initialColor = this.host.getAttribute('color');
  readonly appDisabledButtonColor = input(false, { transform: booleanAttribute });

  constructor() {
    effect(() => {
      if (this.appDisabledButtonColor()) {
        this.renderer.setAttribute(this.host, 'color', 'medium');
        return;
      }

      if (this.initialColor === null) {
        this.renderer.removeAttribute(this.host, 'color');
        return;
      }

      this.renderer.setAttribute(this.host, 'color', this.initialColor);
    });
  }
}
