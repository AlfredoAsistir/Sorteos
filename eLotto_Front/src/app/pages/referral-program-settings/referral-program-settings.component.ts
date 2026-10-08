import { ServerLocalDatePipe } from '../../core/date/server-local-date.pipe';
import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { finalize } from 'rxjs/operators';
import { NumericInputComponent } from '../../components/numeric-input/numeric-input.component';
import { FormNavigationDirective } from '../../core/forms/form-navigation.directive';
import { NotificationService } from '../../core/notifications/notification.service';
import {
  ReferralProgramSettings,
  UpdateReferralProgramSettingsRequest,
} from '../../core/referrals/referral-program-settings.models';
import { MaterialModule } from '../../material.module';
import { LoadingService } from '../../services/loading.service';
import { ReferralProgramSettingsService } from '../../services/referral-program-settings.service';

@Component({
  selector: 'app-referral-program-settings',
  standalone: true,
  imports: [
    ServerLocalDatePipe,
    ReactiveFormsModule,
    MaterialModule,
    NumericInputComponent,
    FormNavigationDirective,
  ],
  templateUrl: './referral-program-settings.component.html',
  styleUrl: './referral-program-settings.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReferralProgramSettingsComponent implements OnInit {
  readonly form = this.fb.nonNullable.group({
    isActive: [true],
    depositRewardPercentage: [0, [Validators.required, Validators.min(0), Validators.max(100)]],
    maxRewardedDeposits: [0, [Validators.required, Validators.min(0)]],
    winnerCashRewardAmount: [0, [Validators.required, Validators.min(0)]],
    minimumConfirmedTickets: [0, [Validators.required, Validators.min(0)]],
  });

  settings: ReferralProgramSettings | null = null;
  loading = false;
  saving = false;

  constructor(
    private readonly fb: FormBuilder,
    private readonly service: ReferralProgramSettingsService,
    private readonly notifications: NotificationService,
    private readonly globalLoading: LoadingService,
    private readonly changeDetector: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    if (this.loading) return;
    this.loading = true;
    this.globalLoading.show();
    this.service.get()
      .pipe(finalize(() => {
        this.loading = false;
        this.globalLoading.hide();
        this.changeDetector.markForCheck();
      }))
      .subscribe({
        next: settings => this.show(settings),
        error: error => this.notifications.show(
          'Error',
          this.errorMessage(error, 'No fue posible cargar la configuración de referidos.'),
          'error'
        ),
      });
  }

  save(): void {
    if (!this.settings || this.form.invalid || !this.form.dirty || this.saving) {
      this.form.markAllAsTouched();
      return;
    }

    const values = this.form.getRawValue();
    const request: UpdateReferralProgramSettingsRequest = {
      ...values,
      rowVersion: this.settings.rowVersion,
    };

    this.saving = true;
    this.globalLoading.show();
    this.service.update(request)
      .pipe(finalize(() => {
        this.saving = false;
        this.globalLoading.hide();
        this.changeDetector.markForCheck();
      }))
      .subscribe({
        next: settings => {
          this.show(settings);
          this.notifications.show(
            'Operación correcta',
            'La configuración del programa de referidos fue actualizada.',
            'success'
          );
        },
        error: error => {
          if (error instanceof HttpErrorResponse && error.status === 409) {
            this.notifications.show(
              'Configuración desactualizada',
              'Otro administrador modificó esta configuración. Se cargarán los valores vigentes.',
              'warning'
            );
            this.load();
            return;
          }

          this.notifications.show(
            'Error',
            this.errorMessage(error, 'No fue posible actualizar la configuración de referidos.'),
            'error'
          );
        },
      });
  }

  undo(): void {
    if (this.settings) this.show(this.settings);
  }

  private show(settings: ReferralProgramSettings): void {
    this.settings = settings;
    this.form.reset({
      isActive: settings.isActive,
      depositRewardPercentage: settings.depositRewardPercentage,
      maxRewardedDeposits: settings.maxRewardedDeposits,
      winnerCashRewardAmount: settings.winnerCashRewardAmount,
      minimumConfirmedTickets: settings.minimumConfirmedTickets,
    });
    this.form.markAsPristine();
    this.changeDetector.markForCheck();
  }

  private errorMessage(error: unknown, fallback: string): string {
    if (!(error instanceof HttpErrorResponse)) return fallback;
    if (typeof error.error?.message === 'string') return error.error.message;

    const validationErrors = error.error?.errors as Record<string, string[]> | undefined;
    const firstMessage = validationErrors
      ? Object.values(validationErrors).flat().find(message => Boolean(message))
      : undefined;
    return firstMessage ?? fallback;
  }
}
