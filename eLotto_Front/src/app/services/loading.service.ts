import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

@Injectable({
  providedIn: 'root',
})
export class LoadingService {
  private readonly _loading = new BehaviorSubject<boolean>(false);
  private readonly _showLuckyNumbers = new BehaviorSubject<boolean>(false);

  readonly loading$ = this._loading.asObservable();
  readonly showLuckyNumbers$ = this._showLuckyNumbers.asObservable();

  show(showLuckyNumbers = false): void {
    this._showLuckyNumbers.next(showLuckyNumbers);
    this._loading.next(true);
  }

  hide(): void {
    this._loading.next(false);
    this._showLuckyNumbers.next(false);
  }
}