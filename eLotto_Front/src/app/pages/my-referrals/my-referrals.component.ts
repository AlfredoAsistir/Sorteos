import { CommonModule } from '@angular/common';
import { ServerLocalDatePipe } from '../../core/date/server-local-date.pipe';
import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { RouterModule } from '@angular/router';
import { finalize } from 'rxjs';
import { NotificationService } from '../../core/notifications/notification.service';
import { MyReferrals } from '../../core/referrals/my-referrals.models';
import { MaterialModule } from '../../material.module';
import { LoadingService } from '../../services/loading.service';
import { MyReferralsService } from '../../services/my-referrals.service';

@Component({
  selector: 'app-my-referrals',
  standalone: true,
  imports: [CommonModule, ServerLocalDatePipe, RouterModule, MaterialModule],
  templateUrl: './my-referrals.component.html',
  styleUrl: './my-referrals.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MyReferralsComponent implements OnInit, OnDestroy {
  data: MyReferrals | null = null;
  loaded = false;

  constructor(
    private readonly service: MyReferralsService,
    private readonly loading: LoadingService,
    private readonly notifications: NotificationService,
    private readonly changeDetector: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loading.show();
    this.service.get()
      .pipe(finalize(() => {
        this.loaded = true;
        this.loading.hide();
        this.changeDetector.markForCheck();
      }))
      .subscribe({
        next: data => {
          this.data = data;
          this.changeDetector.markForCheck();
        },
        error: () => this.notifications.show(
          'No fue posible cargar tus referidos',
          'Intenta nuevamente en unos momentos.',
          'error'
        ),
      });
  }

  ngOnDestroy(): void {
    this.loading.hide();
  }
}
