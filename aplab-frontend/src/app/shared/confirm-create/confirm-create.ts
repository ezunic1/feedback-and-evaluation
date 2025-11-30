import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-confirm-create',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './confirm-create.html',
  styleUrls: ['./confirm-create.css']
})
export class ConfirmCreate {
  @Input() title = 'Are you sure?';
  @Input() message = "You won't be able to delete this feedback after.";
  @Input() confirmLabel = 'Yes';
  @Input() cancelLabel = 'No';
  @Input() loading = false;

  @Output() confirm = new EventEmitter<void>();
  @Output() cancel = new EventEmitter<void>();

  onConfirm() { if (!this.loading) this.confirm.emit(); }
  onCancel() { if (!this.loading) this.cancel.emit(); }
}
