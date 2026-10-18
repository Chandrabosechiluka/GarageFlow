import { AfterViewInit, Component, ElementRef, EventEmitter, HostListener, Input, Output, inject } from '@angular/core';

/** A dialog over the page. Escape or a click on the backdrop closes it; the first field gets the focus. */
@Component({
  selector: 'gf-modal',
  standalone: true,
  template: `
    <div class="backdrop" (click)="closed.emit()"></div>
    <section class="modal" role="dialog" aria-modal="true" [attr.aria-label]="heading">
      <header class="modal-head">
        <h2>{{ heading }}</h2>
        <button type="button" class="icon-btn" aria-label="Close" (click)="closed.emit()">×</button>
      </header>
      <div class="modal-body"><ng-content></ng-content></div>
    </section>
  `
})
export class ModalComponent implements AfterViewInit {
  @Input() heading = '';
  @Output() closed = new EventEmitter<void>();

  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  ngAfterViewInit(): void {
    const first = this.host.nativeElement.querySelector<HTMLElement>('input:not([type=hidden]), select, textarea, .modal-body button');
    first?.focus();
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closed.emit();
  }
}
