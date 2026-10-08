import { Component, EventEmitter, Input, OnInit, Output, ChangeDetectionStrategy } from '@angular/core';
import { BrandingComponent } from './branding.component';
import { TablerIconsModule } from '@luoxiao123/angular-tabler-icons';
import { MaterialModule } from 'src/app/material.module';

@Component({
    selector: 'app-sidebar',
    imports: [BrandingComponent, TablerIconsModule, MaterialModule],
    changeDetection: ChangeDetectionStrategy.Eager,
    templateUrl: './sidebar.component.html'
})

export class SidebarComponent implements OnInit {
  constructor() { }
  toggle = false;
  @Input() showToggle = true; 
  @Output() toggleMobileNav = new EventEmitter<void>();
  @Output() toggleCollapsed = new EventEmitter<void>();

   

  ngOnInit(): void { }
}