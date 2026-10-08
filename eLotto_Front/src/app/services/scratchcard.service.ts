import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  ScratchcardListResponse,
  ScratchcardRevealResponse,
  ScratchcardStartResponse,
} from '../core/scratchcards/scratchcard.models';

@Injectable({ providedIn: 'root' })
export class ScratchcardService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/api/user-scratchcards`;

  getScratchcards(): Observable<ScratchcardListResponse> {
    return this.http.get<ScratchcardListResponse>(this.apiUrl);
  }

  start(scratchcardId: number): Observable<ScratchcardStartResponse> {
    return this.http.post<ScratchcardStartResponse>(
      this.apiUrl + '/' + scratchcardId + '/start',
      {}
    );
  }
  reveal(scratchcardId: number): Observable<ScratchcardRevealResponse> {
    return this.http.post<ScratchcardRevealResponse>(
      `${this.apiUrl}/${scratchcardId}/reveal`,
      {}
    );
  }
}
