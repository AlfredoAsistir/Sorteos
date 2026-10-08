import { Component } from '@angular/core';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MaterialModule } from '../../material.module';

export type AuthRequiredAction = 'login' | 'register';

@Component({
  selector: 'app-auth-required-dialog',
  standalone: true,
  imports: [MatDialogModule, MaterialModule],
  template: `
    <div class="auth-required-dialog">
      <div class="dialog-icon"><mat-icon>confirmation_number</mat-icon></div>
      <h2 mat-dialog-title>Inicia sesión para elegir tus boletos</h2>
      <mat-dialog-content>
        Para generar o consultar números necesitas iniciar sesión.
      </mat-dialog-content>
      <mat-dialog-actions align="center">
        <button mat-stroked-button color="success" type="button" (click)="close('register')">Crear mi cuenta</button>
        <button mat-flat-button color="primary" type="button" (click)="close('login')">Iniciar sesión</button>
      </mat-dialog-actions>
    </div>
  `,
  styles: [`
    .auth-required-dialog { width:min(440px,90vw);padding:22px 24px 18px;box-sizing:border-box;text-align:center }
    .dialog-icon { width:58px;height:58px;margin:0 auto 8px;border-radius:50%;display:grid;place-items:center;color:#1976d2;background:rgba(25,118,210,.12) }
    .dialog-icon mat-icon { width:32px;height:32px;font-size:32px }
    h2 { margin:0;text-align:center }
    mat-dialog-content { max-width:360px;margin:0 auto;line-height:1.55;opacity:.82 }
    mat-dialog-actions { display:flex;gap:12px;padding-top:22px }
    mat-dialog-actions button { min-width:145px }
    @media(max-width:480px){.auth-required-dialog{padding-inline:16px}mat-dialog-actions{align-items:stretch;flex-direction:column-reverse}mat-dialog-actions button{width:100%}}
  `]
})
export class AuthRequiredDialogComponent {
  constructor(private readonly dialogRef: MatDialogRef<AuthRequiredDialogComponent, AuthRequiredAction>) {}

  close(action: AuthRequiredAction): void {
    this.dialogRef.close(action);
  }
}