import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { MyReferrals } from '../core/referrals/my-referrals.models';

@Injectable({ providedIn: 'root' })
export class MyReferralsService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/api/my-referrals`;

  get(): Observable<MyReferrals> {
    return this.http.get<MyReferrals>(this.apiUrl);
  }
}
