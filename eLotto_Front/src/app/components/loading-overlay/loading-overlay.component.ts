import { ChangeDetectionStrategy, Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LoadingService } from '../../services/loading.service';

@Component({
  selector: 'app-loading-overlay',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if(loadingService.loading$ | async) {
      <div class="overlay" role="status" aria-live="polite" aria-label="Procesando">
        <div class="loading-content">
          @if(loadingService.showLuckyNumbers$ | async) {
            <img
              class="lucky-number-animation"
              src="assets/images/lucky-number-loading.gif"
              alt="Generando números de la suerte"
            />
          }
          <div class="spinner" aria-hidden="true"></div>
        </div>
      </div>
    }
  `,
  changeDetection: ChangeDetectionStrategy.Eager,
  styles: [`
    .overlay {
      position: fixed;
      inset: 0;
      width: 100vw;
      height: 100vh;
      padding: 16px;
      box-sizing: border-box;
      background-color: rgba(0, 0, 0, 0.58);
      z-index: 9999;
      display: flex;
      justify-content: center;
      align-items: center;
    }

    .loading-content {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 20px;
      max-width: 100%;
    }

    .lucky-number-animation {
      display: block;
      width: min(360px, calc(100vw - 48px));
      max-height: 42vh;
      object-fit: contain;
      mix-blend-mode: screen;
    }

    .spinner {
      border: 8px solid #f3f3f3;
      border-top: 8px solid #3f51b5;
      border-radius: 50%;
      width: 60px;
      height: 60px;
      box-sizing: border-box;
      animation: spin 1s linear infinite;
    }

    @media (max-width: 600px) {
      .loading-content { gap: 14px; }
      .lucky-number-animation {
        width: min(280px, calc(100vw - 40px));
        max-height: 36vh;
      }
      .spinner {
        width: 48px;
        height: 48px;
        border-width: 6px;
        border-top-width: 6px;
      }
    }

    @keyframes spin {
      0% { transform: rotate(0deg); }
      100% { transform: rotate(360deg); }
    }
  `]
})
export class LoadingOverlayComponent {
  constructor(public loadingService: LoadingService) {}
}