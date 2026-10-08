import { Component, Inject, ChangeDetectionStrategy } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';


@Component({
  standalone: true,
  imports: [MatDialogModule],
  template: `
    <div class="dialog-container">
      <div class="dialog-icon">
        <span class="material-icons error-icon">error_outline</span>
      </div>
      <h2 mat-dialog-title class="dialog-title">{{ data.title }}</h2>
      <mat-dialog-content class="dialog-content">
        {{ data.message }}
      </mat-dialog-content>
      <mat-dialog-actions align="center">
        <button mat-flat-button mat-dialog-close cdkfocusinitial mat-ripple-loader-class-name="mat-mdc-button-ripple" class="close-button bg-error text-white mdc-button mdc-button--unelevated mat-mdc-unelevated-button mat-unthemed mat-mdc-button-base" type="button"><span class="mat-mdc-button-persistent-ripple mdc-button__ripple"></span><span class="mdc-button__label">Aceptar</span><span class="mat-focus-indicator"></span><span class="mat-mdc-button-touch-target"></span><span class="mat-ripple mat-mdc-button-ripple"></span></button>
      </mat-dialog-actions>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.Eager,
  styles: [`
    .dialog-container {
      text-align: center;
      padding: 20px;
    }

    .dialog-icon {
      font-size: 48px;
      color: #e53935;
      margin-bottom: 8px;
    }

    .error-icon {
      font-size: 48px;
    }

    .dialog-title {
      margin: 0;
      font-weight: 600;
    }

    .dialog-content {
      margin-top: 10px;
      font-size: 16px;
      color: #fc4b6c;
    }
      .close-button {
  padding: 8px 20px;
  border-radius: 6px;
  font-weight: 600;
  font-size: 14px;
  text-transform: uppercase;
}
  `]
})
export class ErrorDialogComponent {
  constructor(@Inject(MAT_DIALOG_DATA) public data: { title: string; message: string }) {}
}