import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { UserPrizeHistory } from '../core/winnings/user-prize.models';

@Injectable({ providedIn: 'root' })
export class UserPrizeService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/api/user-prizes`;

  getHistory(): Observable<UserPrizeHistory> {
    return this.http.get<UserPrizeHistory>(this.apiUrl);
  }
}