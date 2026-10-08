import { Injectable } from '@angular/core';
import { MatDialog, MatDialogRef } from '@angular/material/dialog';
import { firstValueFrom } from 'rxjs';
import { SuccessDialogComponent, SystemDialogDetails, SystemDialogType } from '../../components/success-dialog/success-dialog.component';

export type InstallInstructionPlatform = 'android' | 'ios-safari' | 'ios-other-browser';

@Injectable({ providedIn: 'root' })
export class NotificationService {
  constructor(private readonly dialog: MatDialog) {}

  show(
    title: string,
    message: string,
    type: SystemDialogType = 'info',
    details?: SystemDialogDetails
  ): MatDialogRef<SuccessDialogComponent> {
    return this.dialog.open(SuccessDialogComponent, { data: { title, message, type, ...details } });
  }

  async confirm(
    title: string,
    message: string,
    confirmButtonText: string,
    cancelButtonText: string,
    dangerous = false
  ): Promise<boolean> {
    const dialogRef = this.dialog.open(SuccessDialogComponent, {
      data: { title, message, confirmation: true, dangerous, confirmButtonText, cancelButtonText }
    });

    return await firstValueFrom(dialogRef.afterClosed()) === true;
  }
  async confirmDeletion(itemName: string, title = '¿Eliminar registro?'): Promise<boolean> {
    const dialogRef = this.dialog.open(SuccessDialogComponent, {
      data: {
        title,
        message: `Se eliminará ${itemName}. Esta acción no se puede deshacer.`,
        confirmation: true,
        dangerous: true,
        confirmButtonText: 'Si, Eliminar',
        cancelButtonText: 'No, Abortar'
      }
    });

    return await firstValueFrom(dialogRef.afterClosed()) === true;
  }

  showInstallInstructions(platform: InstallInstructionPlatform): MatDialogRef<SuccessDialogComponent> {
    if (platform === 'android') {
      return this.show('Instalar Sorteos GB', '', 'info', {
        steps: [
          'Abre Sorteos GB en Google Chrome.',
          'Pulsa el menú de tres puntos ⋮.',
          'Selecciona “Instalar aplicación” o “Añadir a pantalla de inicio”.',
          'Confirma con “Instalar” o “Añadir”.'
        ],
        note: 'El nombre de la opción puede variar según la versión de Chrome.'
      });
    }

    const steps = [
      ...(platform === 'ios-other-browser' ? ['Abre esta página en Safari.'] : []),
      'Pulsa el menú de página y después “Compartir”. El botón Compartir también puede aparecer directamente.',
      'Desplázate y selecciona “Agregar a pantalla de inicio”.',
      'Activa “Abrir como app web”, si aparece.',
      'Pulsa “Agregar”.'
    ];
    return this.show('Instalar Sorteos GB en iPhone o iPad', '', 'info', {
      steps,
      note: 'Si no aparece “Agregar a pantalla de inicio”, entra en “Editar acciones” para habilitarla.'
    });
  }
}
