import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export type ProblemErrors = Record<string, string[]>;
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  errors?: ProblemErrors;
  traceId?: string;
  [k: string]: any;
}

@Component({
  selector: 'app-server-errors',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './server-errors.html',
  styleUrls: ['./server-errors.css']
})
export class ServerErrors {
  @Input() problem: ProblemDetails | null = null;
  @Input() field?: string | null;

  get hasAnything(): boolean {
    return !!this.problem && (!!this.problem.detail || this.allErrors.length > 0 || !!this.problem.title);
  }

  get allErrors(): string[] {
    const p = this.problem;
    const out: string[] = [];
    if (!p?.errors) return out;
    for (const k of Object.keys(p.errors)) {
      const arr = p.errors[k] || [];
      for (const m of arr) if (m && typeof m === 'string') out.push(m);
    }
    return out;
  }

  get fieldErrors(): string[] {
    const p = this.problem;
    if (!p?.errors || !this.field) return [];
    const key = Object.keys(p.errors).find(k => k.toLowerCase() === this.field!.toLowerCase());
    if (!key) return [];
    const arr = p.errors[key] || [];
    return arr.filter(x => !!x);
  }
}
