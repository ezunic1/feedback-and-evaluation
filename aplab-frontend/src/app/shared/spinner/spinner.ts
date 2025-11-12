import { Component, Input } from '@angular/core';

@Component({
  selector: 'ui-spinner',
  standalone: true,
  template: `<span class="ui-spinner" [style.width.px]="size" [style.height.px]="size" [style.borderWidth.px]="thickness" [style.color]="color" aria-hidden="true"></span>`,
  styleUrls: ['./spinner.css']
})
export class Spinner {
  @Input() size = 14;
  @Input() thickness = 2;
  @Input() color = 'currentColor';
}
