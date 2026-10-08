import { Component, ChangeDetectionStrategy } from '@angular/core';
import { RouterModule } from '@angular/router';
import { MaterialModule } from '../../../material.module';
import { MatButtonModule } from '@angular/material/button';

@Component({
    selector: 'app-error',
    imports: [RouterModule, MaterialModule, MatButtonModule],
    changeDetection: ChangeDetectionStrategy.Eager,
    templateUrl: './error.component.html'
})
export class AppErrorComponent {}
