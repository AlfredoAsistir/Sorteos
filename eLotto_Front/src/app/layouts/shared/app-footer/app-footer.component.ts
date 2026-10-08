import { Component } from '@angular/core';
import { RouterModule } from '@angular/router';
import { appInformation } from '../../../core/config/app-information';
import { MaterialModule } from '../../../material.module';

@Component({
  selector: 'app-footer',
  standalone: true,
  imports: [MaterialModule, RouterModule],
  templateUrl: './app-footer.component.html',
  styleUrl: './app-footer.component.scss'
})
export class AppFooterComponent {
  readonly appInformation = appInformation;
}
