import { Component, ChangeDetectionStrategy } from '@angular/core';
import { CoreService } from 'src/app/services/core.service';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-branding',
  imports: [RouterModule],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: '',
})
export class BrandingComponent { 
  options = this.settings.getOptions();
  constructor(private settings: CoreService) {} 
}
