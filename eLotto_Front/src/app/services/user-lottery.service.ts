import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { CurrentLottery, CurrentUserTickets, ManualTicketQueryResponse, PreReserveResponse, RandomTicketType, TicketPurchaseResponse, TicketWhatsAppResend } from '../core/user-lottery/user-lottery.models';

@Injectable({ providedIn: 'root' })
export class UserLotteryService {
  private readonly apiUrl = `${environment.apiUrl}/api/user-lottery`;
  constructor(private readonly http: HttpClient) {}

  getCurrent(): Observable<CurrentLottery> { return this.http.get<CurrentLottery>(`${this.apiUrl}/current`); }
  getMyTickets(): Observable<CurrentUserTickets> { return this.http.get<CurrentUserTickets>(`${this.apiUrl}/my-tickets`); }
  resendTicketsByWhatsApp(folioCompra: string): Observable<TicketWhatsAppResend> {
    return this.http.post<TicketWhatsAppResend>(`${this.apiUrl}/my-tickets/${encodeURIComponent(folioCompra)}/whatsapp`, {});
  }
  queryManual(numeros: string): Observable<ManualTicketQueryResponse> { return this.http.post<ManualTicketQueryResponse>(`${this.apiUrl}/manual-query`, { numeros }); }
  preReserveManual(numeros: string[], folioCompra: string): Observable<PreReserveResponse> { return this.http.post<PreReserveResponse>(`${this.apiUrl}/pre-reserve/manual`, { numeros, folioCompra }); }
  preReserveRandom(cantidad: number, tipo: RandomTicketType, valor: string): Observable<PreReserveResponse> { return this.http.post<PreReserveResponse>(`${this.apiUrl}/pre-reserve/random`, { cantidad, tipo, valor }); }
  purchase(sorteoId: number, folioCompra: string, numeros: string[]): Observable<TicketPurchaseResponse> { return this.http.post<TicketPurchaseResponse>(this.apiUrl + '/purchase', { sorteoId, folioCompra, numeros }); }
  release(): Observable<void> { return this.http.delete<void>(`${this.apiUrl}/pre-reserve`); }
  imageUrl(path: string): string { return `${environment.apiUrl}${path}`; }
}
