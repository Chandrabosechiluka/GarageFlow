import { Component, EventEmitter, Input, Output } from '@angular/core';

@Component({
  selector: 'gf-pager',
  standalone: true,
  template: `
    @if (totalPages > 1 || totalCount > 0) {
      <nav class="pager" aria-label="Pages">
        <span class="pager-count">{{ rangeText }}</span>
        <span class="pager-buttons">
          <button type="button" class="btn ghost sm" [disabled]="page <= 1" (click)="pageChange.emit(page - 1)">Previous</button>
          <span class="pager-page">Page {{ page }} of {{ totalPages || 1 }}</span>
          <button type="button" class="btn ghost sm" [disabled]="page >= totalPages" (click)="pageChange.emit(page + 1)">Next</button>
        </span>
      </nav>
    }
  `
})
export class PagerComponent {
  @Input() page = 1;
  @Input() pageSize = 20;
  @Input() totalPages = 0;
  @Input() totalCount = 0;
  @Output() pageChange = new EventEmitter<number>();

  get rangeText(): string {
    if (this.totalCount === 0) return 'No results';
    const first = (this.page - 1) * this.pageSize + 1;
    const last = Math.min(this.page * this.pageSize, this.totalCount);
    return `${first}–${last} of ${this.totalCount}`;
  }
}
