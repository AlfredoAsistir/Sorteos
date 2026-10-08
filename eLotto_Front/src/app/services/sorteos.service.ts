import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  FinalizedLotteryWinner,
  Sorteo,
  SorteoFinalizationResult,
  SorteoPage,
  SorteoRescheduleResult,
  SorteoWinnerVerification
} from '../core/sorteos/sorteo.models';

@Injectable({ providedIn: 'root' })
export class SorteosService {
  private readonly apiUrl = `${environment.apiUrl}/Sorteos`;
  constructor(private readonly http: HttpClient) {}
  getServerClock(): Observable<{ localDateTime: string }> {
    return this.http.get<{ localDateTime: string }>(`${environment.apiUrl}/api/server-clock`);
  }
  getPage(page: number): Observable<SorteoPage> {
    return this.http.get<SorteoPage>(this.apiUrl, { params: { page } });
  }
  create(form: FormData): Observable<Sorteo> {
    return this.http.post<Sorteo>(this.apiUrl, form);
  }
  update(id: number, form: FormData): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}`, form);
  }
  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
  deleteScratchcardPrize(sorteoId: number, prizeId: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${sorteoId}/rascadito-premios/${prizeId}`);
  }
  deleteOptionalImage(id: number, imageNumber: 2 | 3): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}/imagenes/${imageNumber}`);
  }
  verifyWinningNumber(sorteoId: number, numeroGanador: string): Observable<SorteoWinnerVerification> {
    return this.http.post<SorteoWinnerVerification>(
      `${this.apiUrl}/${sorteoId}/resultado/verificar`,
      { numeroGanador }
    );
  }
  rescheduleWithoutWinner(sorteoId: number, numeroGanador: string | null): Observable<SorteoRescheduleResult> {
    return this.http.post<SorteoRescheduleResult>(
      `${this.apiUrl}/${sorteoId}/resultado/reprogramar`,
      numeroGanador ? { numeroGanador } : {}
    );
  }
  finalizeWinner(sorteoId: number, numeroGanador: string): Observable<SorteoFinalizationResult> {
    return this.http.post<SorteoFinalizationResult>(
      `${this.apiUrl}/${sorteoId}/resultado/finalizar`,
      { numeroGanador }
    );
  }
  getLatestFinalizedWinner(): Observable<FinalizedLotteryWinner> {
    return this.http.get<FinalizedLotteryWinner>(`${this.apiUrl}/resultado/ultimo-finalizado`);
  }
  imageUrl(path: string | null): string | null {
    return path ? `${environment.apiUrl}${path}` : null;
  }
}


