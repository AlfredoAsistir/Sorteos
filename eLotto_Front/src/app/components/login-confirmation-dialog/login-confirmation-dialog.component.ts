
import { Component, Inject, OnDestroy, ChangeDetectionStrategy } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { Subscription, interval } from 'rxjs';
import { LoginRequest, LoginResponse } from '../../core/auth/auth.models';
import { MaterialModule } from '../../material.module';
import { AuthService } from '../../services/auth.service';

export interface LoginConfirmationDialogData {
  credentials: LoginRequest;
  maskedWhatsApp: string;
  initialMessage?: string;
}

@Component({
  selector: 'app-login-confirmation-dialog',
  standalone: true,
  imports: [ReactiveFormsModule, MatDialogModule, MaterialModule],
  template: `
    <div class="confirmation-dialog">
      <div class="confirmation-icon">📲</div>
      <h2 mat-dialog-title>Confirma tu WhatsApp</h2>

      <mat-dialog-content>
        <p>
          Enviamos un PIN de 4 números al WhatsApp
          <strong>{{ data.maskedWhatsApp }}</strong>.
        </p>

        <mat-form-field appearance="outline" class="w-100">
          <mat-label>PIN de 4 números</mat-label>
          <input
            matInput
            inputmode="numeric"
            maxlength="4"
            autocomplete="one-time-code"
            [formControl]="pin"
            (input)="sanitizePin($event)"
            (keyup.enter)="confirm()"
          />
          @if (pin.touched && pin.invalid) {
            <mat-hint class="text-error">Ingresa los 4 números recibidos.</mat-hint>
          }
        </mat-form-field>

        @if (message) {
          <p class="status-message" [class.text-error]="hasError">{{ message }}</p>
        }

        <div class="resend-area">
          @if (secondsRemaining > 0) {
            <span>Podrás reenviar el PIN en {{ secondsRemaining }} segundos.</span>
          } @else {
            <button mat-button color="primary" type="button" [disabled]="isSending" (click)="resend()">
              Reenviar PIN
            </button>
          }
        </div>
      </mat-dialog-content>

      <mat-dialog-actions align="end">
        <button mat-button type="button" [disabled]="isSending" (click)="close()">Cancelar</button>
        <button
          mat-flat-button
          color="primary"
          type="button"
          [disabled]="pin.invalid || isSending"
          (click)="confirm()"
        >
          {{ isSending ? 'Validando…' : 'Confirmar y entrar' }}
        </button>
      </mat-dialog-actions>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.Eager,
  styles: [`
    .confirmation-dialog { max-width: 420px; text-align: center; }
    .confirmation-icon { font-size: 48px; margin-top: 16px; }
    mat-dialog-content p { line-height: 1.5; }
    input { text-align: center; font-size: 24px; letter-spacing: 12px; }
    .resend-area { min-height: 40px; display: flex; align-items: center; justify-content: center; }
    .status-message { margin-top: 4px; }
    .text-error { color: #d32f2f; }
  `],
})
export class LoginConfirmationDialogComponent implements OnDestroy {
  readonly pin = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.pattern(/^\d{4}$/)],
  });

  secondsRemaining = 30;
  isSending = false;
  message = '';
  hasError = false;
  private timerSubscription?: Subscription;

  constructor(
    @Inject(MAT_DIALOG_DATA) public readonly data: LoginConfirmationDialogData,
    private readonly dialogRef: MatDialogRef<LoginConfirmationDialogComponent, LoginResponse | null>,
    private readonly authService: AuthService
  ) {
    this.message = data.initialMessage ?? '';
    this.startTimer();
  }

  ngOnDestroy(): void {
    this.timerSubscription?.unsubscribe();
  }

  sanitizePin(event: Event): void {
    const input = event.target as HTMLInputElement;
    const sanitized = input.value.replace(/\D/g, '').slice(0, 4);
    input.value = sanitized;
    this.pin.setValue(sanitized);
  }

  confirm(): void {
    if (this.pin.invalid || this.isSending) {
      this.pin.markAsTouched();
      return;
    }

    this.isSending = true;
    this.message = '';
    this.authService.confirmLogin({ ...this.data.credentials, pin: this.pin.value }).subscribe({
      next: (response) => this.dialogRef.close(response),
      error: (error) => {
        this.isSending = false;
        this.hasError = true;
        this.message = error.error?.message ?? 'No fue posible validar el PIN.';
        this.pin.setValue('');
      },
    });
  }

  resend(): void {
    if (this.secondsRemaining > 0 || this.isSending) {
      return;
    }

    this.isSending = true;
    this.message = '';
    this.authService.resendLoginConfirmation(this.data.credentials).subscribe({
      next: (response) => {
        this.isSending = false;
        this.hasError = !response.confirmationSent;
        this.message = response.message ?? '';
        if (response.confirmationSent) {
          this.pin.setValue('');
          this.startTimer();
        }
      },
      error: (error) => {
        this.isSending = false;
        this.hasError = true;
        this.message = error.error?.message ?? 'No fue posible reenviar el PIN.';
      },
    });
  }

  close(): void {
    this.dialogRef.close(null);
  }

  private startTimer(): void {
    this.timerSubscription?.unsubscribe();
    this.secondsRemaining = 30;
    this.timerSubscription = interval(1000).subscribe(() => {
      this.secondsRemaining--;
      if (this.secondsRemaining <= 0) {
        this.secondsRemaining = 0;
        this.timerSubscription?.unsubscribe();
      }
    });
  }
}
