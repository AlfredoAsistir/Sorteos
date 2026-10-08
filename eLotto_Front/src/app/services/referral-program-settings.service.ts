import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  ReferralProgramBenefits,
  ReferralProgramSettings,
  UpdateReferralProgramSettingsRequest,
} from '../core/referrals/referral-program-settings.models';

@Injectable({ providedIn: 'root' })
export class ReferralProgramSettingsService {
  private readonly apiUrl = `${environment.apiUrl}/ReferralProgramSettings`;
  private readonly benefitsApiUrl = `${environment.apiUrl}/ReferralProgramBenefits`;

  constructor(private readonly http: HttpClient) {}

  get(): Observable<ReferralProgramSettings> {
    return this.http.get<ReferralProgramSettings>(this.apiUrl);
  }

  getBenefits(): Observable<ReferralProgramBenefits> {
    return this.http.get<ReferralProgramBenefits>(this.benefitsApiUrl);
  }

  update(request: UpdateReferralProgramSettingsRequest): Observable<ReferralProgramSettings> {
    return this.http.put<ReferralProgramSettings>(this.apiUrl, request);
  }
}
