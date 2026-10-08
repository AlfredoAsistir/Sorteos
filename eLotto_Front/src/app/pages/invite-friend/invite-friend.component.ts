import { DOCUMENT } from '@angular/common';
import { ChangeDetectionStrategy, ChangeDetectorRef, Component, Inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { NotificationService } from '../../core/notifications/notification.service';
import { MaterialModule } from '../../material.module';
import { LoadingService } from '../../services/loading.service';
import { ReferralInvitationService } from '../../services/referral-invitation.service';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-invite-friend',
  standalone: true,
  imports: [FormsModule, RouterModule, MaterialModule],
  templateUrl: './invite-friend.component.html',
  styleUrl: './invite-friend.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InviteFriendComponent implements OnInit {
  readonly defaultInvitationMessage =
    '¡Hola! Te invito a registrarte en Sorteos Global Broker. Usa mi enlace de invitación para crear tu cuenta:';

  referralCode = '';
  invitationLink = '';
  invitationMessage = this.defaultInvitationMessage;
  loading = false;

  constructor(
    private readonly service: ReferralInvitationService,
    private readonly notifications: NotificationService,
    private readonly globalLoading: LoadingService,
    private readonly changeDetector: ChangeDetectorRef,
    @Inject(DOCUMENT) private readonly document: Document
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
        next: invitation => {
          this.referralCode = invitation.referralCode;
          this.invitationLink = this.buildInvitationLink(invitation.referralCode);
          this.changeDetector.markForCheck();
        },
        error: () => this.notifications.show(
          'Error',
          'No fue posible obtener tu información de invitación.',
          'error'
        ),
      });
  }

  async copyLink(): Promise<void> {
    if (!this.invitationLink) return;

    try {
      const clipboard = this.document.defaultView?.navigator.clipboard;
      if (!clipboard) throw new Error('Clipboard API unavailable');

      await clipboard.writeText(this.buildInvitationText());
      this.notifications.show(
        'Invitación copiada',
        'La invitación se copió al portapapeles.',
        'success'
      );
    } catch {
      this.notifications.show(
        'No fue posible copiar',
        'Copia manualmente la invitación y el enlace mostrados en pantalla.',
        'warning'
      );
    }
  }

  openWhatsApp(): void {
    if (!this.invitationLink) return;

    const whatsappUrl = `https://wa.me/?text=${encodeURIComponent(this.buildInvitationText())}`;
    this.document.defaultView?.open(whatsappUrl, '_blank', 'noopener,noreferrer');
  }

  buildInvitationText(): string {
    const message = this.invitationMessage.trim();
    return message ? `${message}\n\n${this.invitationLink}` : this.invitationLink;
  }

  private buildInvitationLink(referralCode: string): string {
    const origin = this.document.location.origin;
    return `${origin}/authentication/side-register?ref=${encodeURIComponent(referralCode)}`;
  }
}
