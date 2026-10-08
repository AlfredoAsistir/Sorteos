import { Component, ChangeDetectionStrategy } from '@angular/core';
import { CoreService } from 'src/app/services/core.service';
import { FormGroup, FormControl, Validators, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MaterialModule } from '../../../material.module';
import { BrandingComponent } from '../../../layouts/full/vertical/sidebar/branding.component';
import { TablerIconsModule } from '@luoxiao123/angular-tabler-icons';
import { AuthService } from 'src/app/services/auth.service'; // 👈 import del servicio

import { LoadingService } from 'src/app/services/loading.service';
import { APP_BRANDING } from '../../../config/branding.config';
import { NotificationService } from '../../../core/notifications/notification.service';
import { RegisterRequest } from '../../../core/auth/auth.models';

interface Country {
  name: string;
  code: string;
  flag: string;
}

@Component({
  selector: 'app-side-register',
  standalone: true,
  imports: [
    RouterModule,
    MaterialModule,
    FormsModule,
    ReactiveFormsModule,
    BrandingComponent,
    TablerIconsModule
],
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './side-register.component.html'//
})

export class AppSideRegisterComponent {
  readonly appName = APP_BRANDING.name;
  passwordVisible = false;
  confirmPasswordVisible = false;
  referralCode: string | null = null;
  countryControl = new FormControl();

  countries: Country[] = [
    { name: 'MX +52', code: '+52', flag: 'https://upload.wikimedia.org/wikipedia/commons/f/fc/Flag_of_Mexico.svg' },
    { name: 'US +1', code: '+1', flag: 'https://upload.wikimedia.org/wikipedia/commons/a/a4/Flag_of_the_United_States.svg' },
    { name: 'CA +1', code: '+1', flag: 'https://upload.wikimedia.org/wikipedia/commons/c/cf/Flag_of_Canada.svg' }
  ];


  filteredCountries = this.countries;
  
  options = this.settings.getOptions();

  constructor(
    private settings: CoreService,
    private router: Router,
    private route: ActivatedRoute,
    private authService: AuthService,
    private notifications: NotificationService,
    private loadingService: LoadingService
  ) {}

  form = new FormGroup({
    name: new FormControl('', [Validators.required]),
    user: new FormControl('', [Validators.required, Validators.minLength(8)]),
    whatsapp: new FormControl('', [Validators.required]),
    password: new FormControl('', [Validators.required, Validators.minLength(8)]),
    confirmPassword: new FormControl('', [Validators.required]),
    country: new FormControl('', [Validators.required]),
    confirmedOver18: new FormControl(false, {
      nonNullable: true,
      validators: [Validators.requiredTrue]
    }),
    referralCode: new FormControl({ value: '', disabled: true })
  });

  ngOnInit(): void {
    const referralCode = this.route.snapshot.queryParamMap.get('ref')?.trim().toUpperCase();
    if (referralCode) {
      this.referralCode = referralCode;
      this.form.controls.referralCode.setValue(referralCode);
    }

    this.form.valueChanges.subscribe(() => {
      this.passwordsMatchValidator(this.form);
    });
  }

  get f() {
    return this.form.controls;
  }

  allowOnlyNumbers(event: KeyboardEvent) {
    const charCode = event.key;
    if (!/^[0-9]$/.test(charCode)) {
      event.preventDefault();
    }
  }

  validateCountry() {
    const value = this.form.get('country')?.value;
    const match = this.filteredCountries.find(c => c.name === value);
    if (!match) {
      this.form.get('country')?.setValue(null); // o ''
      this.form.get('country')?.setErrors({ invalidCountry: true });
    }
  }

  private passwordsMatchValidator(group: FormGroup): void {
    const password = group.get('password')?.value;
    const confirmPassword = group.get('confirmPassword');
    if (!confirmPassword) return;

    const errors = { ...(confirmPassword.errors ?? {}) };
    delete errors['passwordMismatch'];
    if (confirmPassword.value && password !== confirmPassword.value) errors['passwordMismatch'] = true;
    confirmPassword.setErrors(Object.keys(errors).length ? errors : null);
  }

  submit() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const country = this.form.value.country;
    const numericCode = country ? country.match(/\+(\d+)/)?.[1] ?? '' : '';

    const data: RegisterRequest = {
      name: this.form.value.name ?? '',
      user: this.form.value.user ?? '',
      whatsApp: '+' + numericCode + (this.form.value.whatsapp ?? ''),
      password: this.form.value.password ?? '',
      confirmPassword: this.form.value.confirmPassword ?? '',
      device: '',
      confirmedOver18: this.form.controls.confirmedOver18.value,
      ...(this.referralCode ? { referralCode: this.referralCode } : {})
    };

    this.loadingService.show();
    this.authService.register(data).subscribe({
      next: (result) => {
        this.loadingService.hide();
        if (result.ok) {
        const dialogRef = this.notifications.show('¡Registro correcto!', result.message, 'success');
        dialogRef.afterClosed().subscribe(() => {
          this.router.navigate(['/authentication/login']); // redirige al login
        });
        } else {
          this.notifications.show('Error al registrarse', result.message, 'error');
        }
      },
       error: (err) => {
        this.loadingService.hide();
        this.notifications.show(
          'Error al registrarse',
          err.error?.message ?? 'No fue posible conectar con el servidor.',
          'error'
        );
              }
    });
  }
}
