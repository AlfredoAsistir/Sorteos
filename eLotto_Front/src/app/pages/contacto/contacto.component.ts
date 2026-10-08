import { Component } from '@angular/core';
import { MaterialModule } from '../../material.module';
import { APP_BRANDING } from '../../config/branding.config';

@Component({
  selector: 'app-contacto',
  standalone: true,
  imports: [MaterialModule],
  template: `<main class="simple-user-page"><mat-card class="cardWithShadow"><mat-card-content><mat-icon class="contact-icon">support_agent</mat-icon><h1>Contacto</h1><p>Estamos para atenderte en {{ appName }}.</p><div class="contact-options"><a [href]="contact.whatsappUrl" target="_blank" rel="noopener noreferrer"><mat-icon>chat</mat-icon><span><strong>WhatsApp</strong>Escríbenos por WhatsApp</span></a><a [href]="contact.emailUrl"><mat-icon>email</mat-icon><span><strong>Correo electrónico</strong>{{ contact.email }}</span></a></div></mat-card-content></mat-card></main>`,
  styles: [`.simple-user-page{max-width:900px;margin:0 auto;padding:24px 16px}.simple-user-page mat-card-content{text-align:center;padding:48px 24px}.contact-icon{width:56px;height:56px;font-size:56px;color:#1976d2}.simple-user-page h1{margin:14px 0 8px}.simple-user-page p{margin:0;opacity:.7}.contact-options{display:grid;grid-template-columns:repeat(auto-fit,minmax(210px,1fr));gap:16px;margin-top:32px}.contact-options a{display:flex;align-items:center;gap:12px;padding:18px;border:1px solid rgba(127,127,127,.25);border-radius:12px;text-align:left;text-decoration:none;color:inherit}.contact-options a:hover{border-color:#1976d2}.contact-options a>mat-icon{color:#1976d2}.contact-options span{display:flex;flex-direction:column;overflow-wrap:anywhere}.contact-options strong{margin-bottom:4px}`]
})
export class ContactoComponent {
  readonly appName = APP_BRANDING.name;
  readonly contact = APP_BRANDING.contact;
}
