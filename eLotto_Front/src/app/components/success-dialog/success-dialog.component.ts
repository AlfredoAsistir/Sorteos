import { ChangeDetectionStrategy, Component, Inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';

export type SystemDialogType = 'success' | 'error' | 'warning' | 'info';

export interface SystemDialogDetails {
  highlightedMessage?: string;
  detailLabel?: string;
  detailText?: string;
  steps?: readonly string[];
  note?: string;
}

export interface SystemDialogData extends SystemDialogDetails {
  title: string;
  message: string;
  type?: SystemDialogType;
  confirmation?: boolean;
  dangerous?: boolean;
  confirmButtonText?: string;
  cancelButtonText?: string;
}

@Component({
  standalone: true,
  imports: [MatDialogModule],
  template: `
    <div class="dialog-container">
      <div class="dialog-icon" [style.color]="accentColor">
        <span class="material-icons">{{ icon }}</span>
      </div>
      <h2 mat-dialog-title class="dialog-title">{{ data.title }}</h2>
      <mat-dialog-content class="dialog-content" [style.color]="accentColor">
        @if (data.highlightedMessage) {
          <div class="structured-message">
            <p class="highlighted-message">{{ data.highlightedMessage }}</p>
            @if (data.detailLabel) {
              <strong class="detail-label">{{ data.detailLabel }}</strong>
            }
            @if (data.detailText) {
              <p class="detail-text">{{ data.detailText }}</p>
            }
          </div>
        } @else if (data.steps?.length) {
          <ol class="instruction-steps">
            @for (step of data.steps; track $index) {
              <li>{{ step }}</li>
            }
          </ol>
          @if (data.note) {
            <p class="instruction-note">{{ data.note }}</p>
          }
        } @else {
          {{ data.message }}
        }
      </mat-dialog-content>
      <mat-dialog-actions align="center" [class.confirm-actions]="data.confirmation">
        @if (data.confirmation) {
          <button class="dialog-action safe-action" mat-flat-button [mat-dialog-close]="false" cdkFocusInitial type="button">
            {{ data.cancelButtonText }}
          </button>
          <button class="dialog-action" [class.danger-action]="data.dangerous" [class.confirm-action]="!data.dangerous" mat-flat-button [mat-dialog-close]="true" type="button">
            {{ data.confirmButtonText }}
          </button>
        } @else {
          <button class="dialog-action acknowledge-action" mat-flat-button [mat-dialog-close]="true" type="button">
            {{ data.confirmButtonText ?? 'Aceptar' }}
          </button>
        }
      </mat-dialog-actions>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.Eager,
  styles: [`
    .dialog-container { text-align: center; padding: 20px; }
    .dialog-icon { margin-bottom: 8px; }
    .dialog-icon .material-icons { width: 48px; height: 48px; font-size: 48px; }
    .dialog-title { margin: 0; font-weight: 600; }
    .dialog-content { margin-top: 10px; font-size: 16px; }
    .structured-message { text-align: center; }
    .highlighted-message { margin: 0 0 2em; color: #42a5f5; font-weight: 700; }
    .detail-label { display: block; margin-bottom: 10px; color: inherit; }
    .detail-text { max-width: 560px; margin: 0 auto; color: inherit; line-height: 1.55; text-align: center; overflow-wrap: anywhere; }
    .instruction-steps { max-width: 520px; margin: 0 auto; padding-left: 24px; color: var(--mat-sys-on-surface, #1f2937); line-height: 1.5; text-align: left; }
    .instruction-steps li + li { margin-top: 10px; }
    .instruction-note { max-width: 520px; margin: 18px auto 0; color: var(--mat-sys-on-surface-variant, #5f6368); font-size: 14px; line-height: 1.45; text-align: left; }
    .confirm-actions { display: flex; justify-content: center; gap: 14px; flex-wrap: wrap; }
    .dialog-action { min-width: 150px; height: 42px; padding: 0 20px; border-radius: 6px; font-size: 14px; font-weight: 600; text-transform: uppercase; color: #fff; }
    .safe-action { background-color: #1976d2; }
    .danger-action { background-color: #d32f2f; }
    .confirm-action { background-color: #2e7d32; }
    .acknowledge-action { background-color: #1976d2; }
    @media (max-width: 420px) {
      .confirm-actions { align-items: stretch; }
      .dialog-action { flex: 1 1 150px; }
    }
  `]
})
export class SuccessDialogComponent {
  constructor(@Inject(MAT_DIALOG_DATA) public data: SystemDialogData) {}

  get icon(): string {
    if (this.data.confirmation) return 'help_outline';
    const icons: Record<SystemDialogType, string> = {
      success: 'check_circle_outline',
      error: 'error_outline',
      warning: 'warning_amber',
      info: 'info_outline'
    };
    return icons[this.data.type ?? 'info'];
  }

  get accentColor(): string {
    if (this.data.confirmation) return this.data.dangerous ? '#d32f2f' : '#1976d2';
    const colors: Record<SystemDialogType, string> = {
      success: '#2e7d32',
      error: '#d32f2f',
      warning: '#ed6c02',
      info: '#1976d2'
    };
    return colors[this.data.type ?? 'info'];
  }
}