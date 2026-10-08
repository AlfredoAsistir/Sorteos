import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { ReferralInvitation } from '../core/referrals/referral-invitation.models';

@Injectable({ providedIn: 'root' })
export class ReferralInvitationService {
  private readonly apiUrl = `${environment.apiUrl}/ReferralInvitation`;

  constructor(private readonly http: HttpClient) {}

  get(): Observable<ReferralInvitation> {
    return this.http.get<ReferralInvitation>(this.apiUrl);
  }
}
