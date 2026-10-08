import { Component, ChangeDetectionStrategy } from '@angular/core';
import { CoreService } from 'src/app/services/core.service';
import { AuthService } from 'src/app/services/auth.service';
import { FormGroup, FormControl, Validators, FormsModule, ReactiveFormsModule, AbstractControl, ValidationErrors,} from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { MaterialModule } from '../../../material.module';
import { BrandingComponent } from '../../../layouts/full/vertical/sidebar/branding.component';
import { LoadingService } from 'src/app/services/loading.service';
import { NotificationService } from '../../../core/notifications/notification.service';

@Component({
  selector: 'app-side-forgot-password',
  imports: [
    RouterModule,
    MaterialModule,
    FormsModule,
    ReactiveFormsModule,
    BrandingComponent,
  ],
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './side-forgot-password.component.html',
})
export class AppSideForgotPasswordComponent {
  options = this.settings.getOptions();
  pinSent = false;
  newPasswordVisible = false;
  confirmPasswordVisible = false;

  constructor(private settings: CoreService, private router: Router, private authService: AuthService,
    private notifications: NotificationService, private loadingService: LoadingService) {}
  
  get f() {
    return this.form.controls;
  }

  form = new FormGroup({
    whatsApp: new FormControl('', [Validators.required, Validators.pattern(/^\d{10}$/)]),
    pin: new FormControl(''/*, [Validators.required]*/), 
     newPassword: new FormControl(''/*, [Validators.required]*/),
    confirmPassword: new FormControl(''/*, [Validators.required]*/),
  }, { validators: this.passwordsMatchValidator });

  passwordsMatchValidator(group: AbstractControl): ValidationErrors | null {
    const password = group.get('newPassword')?.value;
    const confirm = group.get('confirmPassword')?.value;
    return password === confirm ? null : { passwordsMismatch: true };
  }
  
  submit() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    if(!this.pinSent)
    {
      const whatsApp = this.form.value.whatsApp;

      this.loadingService.show();
      this.authService.sendResetPin(whatsApp ?? '').subscribe({
        next: (result) => {
          this.loadingService.hide();

          if(result.ok)
          {
            this.notifications.show('¡Correcto!', result.message, 'success')
            .afterClosed()
            .subscribe(() => {
              this.pinSent = true;
              this.enablePasswordResetValidators();
            });
        }
        else{
          this.notifications.show('¡Error!', result.message, 'error');
        }

        },
        error: (err) => {
          this.loadingService.hide();
          this.notifications.show(
            '¡Error!',
            err.error?.message ?? 'No fue posible conectar con el servidor.',
            'error'
          );
        }
      });
    }
    else{

      const password = this.form.value.newPassword;
      const confirmPassword = this.form.value.confirmPassword;
      const confirmCode = this.form.value.pin;
      const whatsApp = this.form.value.whatsApp;

      this.loadingService.show();
      this.authService.resetPassword(whatsApp ??'',password ??'',confirmPassword ??'',confirmCode ??'').subscribe({
        next: (result) => {
          this.loadingService.hide();
          if(result.ok)
          {
            this.notifications.show('¡Correcto!', result.message, 'success')
            .afterClosed()
            .subscribe(() => {
              this.router.navigate(['/authentication/login']); // redirige al login
            });
          }
        else{
          this.notifications.show('¡Error!', result.message, 'error');
        }
        },
        error: (err) => {
          this.loadingService.hide();
          this.notifications.show(
            '¡Error!',
            err.error?.message ?? 'No fue posible conectar con el servidor.',
            'error'
          );
        }
      });
    }
  }

  setPIN() {
    this.pinSent = true;
    this.enablePasswordResetValidators();
  }

  private enablePasswordResetValidators(): void {
    this.form.get('pin')?.setValidators([Validators.required, Validators.pattern(/^\d{6}$/)]);
    this.form.get('newPassword')?.setValidators([Validators.required, Validators.minLength(8)]);
    this.form.get('confirmPassword')?.setValidators([Validators.required, Validators.minLength(8)]);

    this.form.get('pin')?.updateValueAndValidity();
    this.form.get('newPassword')?.updateValueAndValidity();
    this.form.get('confirmPassword')?.updateValueAndValidity();
  }
}
