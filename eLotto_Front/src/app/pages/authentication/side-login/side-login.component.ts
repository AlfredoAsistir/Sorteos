import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, ElementRef, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, FormsModule, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { Router, RouterModule } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { TablerIconsModule } from '@luoxiao123/angular-tabler-icons';
import { AuthRequiredAction, AuthRequiredDialogComponent } from '../../../components/auth-required-dialog/auth-required-dialog.component';
import { LoginConfirmationDialogComponent } from '../../../components/login-confirmation-dialog/login-confirmation-dialog.component';
import { LotteryDateStripComponent } from '../../../components/lottery-date-strip/lottery-date-strip.component';
import { NumericInputComponent } from '../../../components/numeric-input/numeric-input.component';
import { SorteoStatusComponent } from '../../../components/sorteo-status/sorteo-status.component';
import { APP_BRANDING } from '../../../config/branding.config';
import { LoginRequest, LoginResponse } from '../../../core/auth/auth.models';
import { NotificationService } from '../../../core/notifications/notification.service';
import { AppInstallationService } from '../../../core/pwa/app-installation.service';
import { CurrentLottery, RandomTicketType } from '../../../core/user-lottery/user-lottery.models';
import { BrandingComponent } from '../../../layouts/full/vertical/sidebar/branding.component';
import { MaterialModule } from '../../../material.module';
import { AuthService } from '../../../services/auth.service';
import { LoadingService } from '../../../services/loading.service';
import { UserLotteryService } from '../../../services/user-lottery.service';
import { isInstalledMobileApp } from './installed-mobile-app';

@Component({
  selector: 'app-side-login',
  imports: [
    CommonModule,
    RouterModule,
    MaterialModule,
    FormsModule,
    ReactiveFormsModule,
    BrandingComponent,
    TablerIconsModule,
    MatDialogModule,
    NumericInputComponent,
    SorteoStatusComponent,
    LotteryDateStripComponent,
  ],
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './side-login.component.html',
  styleUrl: './side-login.component.scss',
})
export class AppSideLoginComponent implements OnInit, OnDestroy {
  readonly appName = APP_BRANDING.name;
  @ViewChild('loginUser') private loginUser?: ElementRef<HTMLInputElement>;
  @ViewChild('loginPassword') private loginPassword?: ElementRef<HTMLInputElement>;

  error = '';
  passwordVisible = false;
  lottery: CurrentLottery | null = null;
  lotteryLoading = true;
  carouselIndex = 0;
  previousCarouselIndex: number | null = null;
  carouselDirection: 'left-to-right' | 'right-to-left' = 'left-to-right';
  lotteryCountdownSeconds = 0;
  private carouselTimerId: number | null = null;
  private carouselTransitionTimerId: number | null = null;
  private lotteryStatusTimerId: number | null = null;
  private lotteryTargetTime = 0;
  private lotterySalesCloseTargetTime = 0;
  private lastBlockedLotteryRefreshAt = 0;
  private blockedLotteryRefreshInProgress = false;
  private destroyed = false;

  readonly lotteryRandomForm = new FormGroup({
    tipo: new FormControl<RandomTicketType>('azar', { nonNullable: true }),
    valor: new FormControl('', { nonNullable: true }),
    cantidad: new FormControl(300, { nonNullable: true, validators: [Validators.required, Validators.min(1), Validators.max(2000)] }),
  });

  readonly lotteryManualForm = new FormGroup({
    numeros: new FormControl('', { nonNullable: true }),
  });

  readonly form = new FormGroup({
    uname: new FormControl('', [Validators.required, this.usernameOrWhatsAppValidator]),
    password: new FormControl('', [Validators.required, Validators.minLength(8)]),
    keepSession: new FormControl(
      typeof window !== 'undefined' && typeof navigator !== 'undefined' && isInstalledMobileApp(window, navigator)
    ),
  });

  constructor(
    private readonly router: Router,
    private readonly authService: AuthService,
    private readonly dialog: MatDialog,
    private readonly notifications: NotificationService,
    private readonly loadingService: LoadingService,
    private readonly userLotteryService: UserLotteryService,
    readonly appInstallation: AppInstallationService,
  ) {}

  installApplication(): void {
    void this.appInstallation.requestInstallation();
  }

  ngOnInit(): void {
    this.userLotteryService.getCurrent().subscribe({
      next: lottery => {
        this.lottery = lottery;
        this.lotteryLoading = false;
        this.startLotteryStatusTimer();
        this.startCarousel();
        if (!lottery.ventaDisponible) void this.refreshBlockedLottery();
      },
      error: () => {
        this.lottery = null;
        this.lotteryLoading = false;
      },
    });
  }

  ngOnDestroy(): void {
    this.destroyed = true;
    this.stopLotteryStatusTimer();
    this.stopCarousel();
    this.stopCarouselTransition();
  }

  get f() {
    return this.form.controls;
  }

  get images(): string[] {
    if (!this.lottery) return [];
    return [this.lottery.imagen1, this.lottery.imagen2, this.lottery.imagen3]
      .filter((image): image is string => !!image)
      .map(image => this.userLotteryService.imageUrl(image));
  }


  usernameOrWhatsAppValidator(control: AbstractControl): ValidationErrors | null {
    const value = (control.value ?? '').trim();
    if (!value) return null;
    if (/^\d+$/.test(value)) return value.length === 10 ? null : { whatsAppLength: true };
    return value.length >= 8 ? null : { usernameLength: true };
  }

  onUsernameEnter(event: Event): void {
    event.preventDefault();
    this.loginPassword?.nativeElement.focus();
  }

  onPasswordEnter(event: Event): void {
    event.preventDefault();
    this.submit();
  }

  promptAuthentication(): void {
    const dialogRef = this.dialog.open(AuthRequiredDialogComponent, {
      width: '460px',
      maxWidth: '95vw',
      autoFocus: false,
    });

    dialogRef.afterClosed().subscribe((action: AuthRequiredAction | undefined) => {
      if (action === 'register') {
        void this.router.navigate(['/authentication/side-register']);
        return;
      }
      if (action === 'login') {
        window.setTimeout(() => {
          this.loginUser?.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'center' });
          this.loginUser?.nativeElement.focus();
        });
      }
    });
  }

  previousImage(): void {
    if (this.images.length) {
      this.transitionTo((this.carouselIndex - 1 + this.images.length) % this.images.length, 'right-to-left');
      this.startCarousel();
    }
  }

  nextImage(): void {
    this.advanceCarousel();
    this.startCarousel();
  }

  selectImage(index: number): void {
    this.transitionTo(index, index < this.carouselIndex ? 'right-to-left' : 'left-to-right');
    this.startCarousel();
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const credentials: LoginRequest = {
      username: this.form.value.uname ?? '',
      password: this.form.value.password ?? '',
      device: '',
    };

    this.loadingService.show();
    this.authService.login(credentials).subscribe({
      next: response => {
        this.loadingService.hide();
        if (response.requiresConfirmation) {
          if (!response.confirmationSent) {
            this.notifications.show(
              'No fue posible validar tu WhatsApp',
              response.message ?? 'No fue posible validar tu número de WhatsApp. Por favor, contáctanos para poder continuar.',
              'error'
            );
            return;
          }
          this.openConfirmationDialog(credentials, response);
          return;
        }
        if (!response.ok || !response.token) {
          this.notifications.show('Error al iniciar sesión', response.message ?? 'No fue posible iniciar sesión.', 'error');
          return;
        }
        this.completeLogin(response);
      },
      error: err => {
        this.loadingService.hide();
        this.notifications.show(
          'Error al iniciar sesión',
          err.error?.message ?? 'No fue posible conectar con el servidor.',
          'error'
        );
      },
    });
  }

  private startCarousel(): void {
    this.stopCarousel();
    if (this.images.length < 2) return;
    this.carouselTimerId = window.setInterval(() => this.advanceCarousel(), 10000);
  }

  private startLotteryStatusTimer(): void {
    this.stopLotteryStatusTimer();
    if (!this.lottery) return;
    this.lotteryTargetTime = performance.now() + Math.max(0, this.lottery.segundosParaInicio) * 1000;
    this.lotterySalesCloseTargetTime = performance.now() + Math.max(0, this.lottery.segundosParaCierreVentas) * 1000;
    const tick = () => {
      if (!this.lottery) return;
      const salesWereOpen = this.lottery.ventaDisponible;
      this.lotteryCountdownSeconds = Math.max(
        0,
        Math.ceil((this.lotteryTargetTime - performance.now()) / 1000)
      );
      this.lottery.segundosParaInicio = this.lotteryCountdownSeconds;
      this.lottery.segundosParaCierreVentas = Math.max(0, Math.ceil((this.lotterySalesCloseTargetTime - performance.now()) / 1000));
      if (this.lotteryCountdownSeconds === 0) {
        this.lottery.estado = 'en_proceso';
        this.lottery.ventaDisponible = false;
      } else if (this.lottery.segundosParaCierreVentas <= 0) {
        this.lottery.estado = 'proximo_a_iniciar';
        this.lottery.ventaDisponible = false;
      }
      if (!this.lottery.ventaDisponible &&
          (salesWereOpen || performance.now() - this.lastBlockedLotteryRefreshAt >= 15_000)) {
        this.lastBlockedLotteryRefreshAt = performance.now();
        void this.refreshBlockedLottery();
      }
    };
    tick();
    this.lotteryStatusTimerId = window.setInterval(tick, 1000);
  }

  private stopLotteryStatusTimer(): void {
    if (this.lotteryStatusTimerId !== null) window.clearInterval(this.lotteryStatusTimerId);
    this.lotteryStatusTimerId = null;
  }

  private async refreshBlockedLottery(): Promise<void> {
    if (this.blockedLotteryRefreshInProgress || !this.lottery || this.lottery.ventaDisponible) return;
    this.blockedLotteryRefreshInProgress = true;
    try {
      const refreshed = await firstValueFrom(this.userLotteryService.getCurrent());
      if (this.destroyed) return;
      const previous = this.lottery;
      if (!previous) return;
      const changed = refreshed.id !== previous.id || refreshed.fecha !== previous.fecha ||
        refreshed.estado !== previous.estado || refreshed.ventaDisponible !== previous.ventaDisponible ||
        refreshed.boletosVendidos !== previous.boletosVendidos ||
        refreshed.porcentajeMinimoVenta !== previous.porcentajeMinimoVenta ||
        refreshed.urlTransmisionEnVivo !== previous.urlTransmisionEnVivo;
      if (changed) {
        const lotteryChanged = refreshed.id !== previous.id || refreshed.fecha !== previous.fecha;
        if (lotteryChanged) this.carouselIndex = 0;
        this.lottery = refreshed;
        this.startLotteryStatusTimer();
        if (lotteryChanged) this.startCarousel();
      }
    } catch {
      // Se conserva el último sorteo hasta el siguiente sondeo.
    } finally {
      this.blockedLotteryRefreshInProgress = false;
    }
  }

  private advanceCarousel(): void {
    if (this.images.length < 2) return;
    this.transitionTo((this.carouselIndex + 1) % this.images.length, 'left-to-right');
  }

  private transitionTo(index: number, direction: 'left-to-right' | 'right-to-left'): void {
    if (index === this.carouselIndex || !this.images[index]) return;
    this.stopCarouselTransition();
    this.previousCarouselIndex = this.carouselIndex;
    this.carouselDirection = direction;
    this.carouselIndex = index;
    this.carouselTransitionTimerId = window.setTimeout(() => {
      this.previousCarouselIndex = null;
      this.carouselTransitionTimerId = null;
    }, 650);
  }

  private stopCarousel(): void {
    if (this.carouselTimerId !== null) window.clearInterval(this.carouselTimerId);
    this.carouselTimerId = null;
  }

  private stopCarouselTransition(): void {
    if (this.carouselTransitionTimerId !== null) window.clearTimeout(this.carouselTransitionTimerId);
    this.carouselTransitionTimerId = null;
    this.previousCarouselIndex = null;
  }

  private openConfirmationDialog(credentials: LoginRequest, response: LoginResponse): void {
    const dialogRef = this.dialog.open(LoginConfirmationDialogComponent, {
      width: '460px',
      maxWidth: '95vw',
      disableClose: true,
      data: {
        credentials,
        maskedWhatsApp: response.maskedWhatsApp ?? '******',
        initialMessage: response.confirmationSent ? '' : response.message,
      },
    });

    dialogRef.afterClosed().subscribe(confirmedLogin => {
      if (confirmedLogin?.token) this.completeLogin(confirmedLogin);
    });
  }

  private completeLogin(response: LoginResponse): void {
    if (!response.token) return;
    this.authService.createSession(response, this.form.value.keepSession ?? false);
    void this.router.navigate(['/starter/index']);
  }
}
