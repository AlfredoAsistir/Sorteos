import { HttpClient, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { NotificationService } from '../../core/notifications/notification.service';
import { CurrentLottery } from '../../core/user-lottery/user-lottery.models';
import { environment } from '../../../environments/environment';
import { SorteoTransparencyComponent } from './sorteo-transparency.component';

describe('SorteoTransparencyComponent', () => {
  let component: SorteoTransparencyComponent;
  let http: HttpTestingController;
  let notifications: jasmine.SpyObj<NotificationService>;
  const lottery = { id: 17, fecha: '2026-09-30T20:00:00' } as CurrentLottery;

  beforeEach(() => {
    notifications = jasmine.createSpyObj<NotificationService>('NotificationService', ['show']);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: NotificationService, useValue: notifications },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    component = new SorteoTransparencyComponent(TestBed.inject(HttpClient), notifications);
    component.lottery = lottery;
    component.ngOnChanges();
  });

  afterEach(() => {
    component.ngOnDestroy();
    http.verify();
  });

  it('solo descarga el documento publicado para la fecha vigente', () => {
    component.downloadPdf();
    http.expectNone(`${environment.apiUrl}/api/sorteo-transparencia/17/pdf`);

    http.expectOne(`${environment.apiUrl}/api/sorteo-transparencia/17`).flush({
      fechaProgramada: lottery.fecha,
      estado: 'publicado',
    });
    spyOn(URL, 'createObjectURL').and.returnValue('blob:transparencia');
    spyOn(HTMLAnchorElement.prototype, 'click').and.stub();
    component.downloadPdf();
    const download = http.expectOne(`${environment.apiUrl}/api/sorteo-transparencia/17/pdf`);
    expect(download.request.responseType).toBe('blob');
    download.flush(new Blob(['%PDF-1.7'], { type: 'application/pdf' }));
    expect(component.downloading).toBeFalse();
    expect(notifications.show).not.toHaveBeenCalled();
  });

  it('oculta el estado anterior y cancela descargas al cambiar la fecha', () => {
    http.expectOne(`${environment.apiUrl}/api/sorteo-transparencia/17`).flush({
      fechaProgramada: lottery.fecha,
      estado: 'publicado',
    });
    component.downloadPdf();
    const download = http.expectOne(`${environment.apiUrl}/api/sorteo-transparencia/17/pdf`);

    component.lottery = { ...lottery, fecha: '2026-10-07T20:00:00' };
    component.ngOnChanges();
    expect(download.cancelled).toBeTrue();
    expect(component.status).toBeNull();
    expect(component.downloading).toBeFalse();

    http.expectOne(`${environment.apiUrl}/api/sorteo-transparencia/17`).flush({
      fechaProgramada: lottery.fecha,
      estado: 'publicado',
    });
    component.downloadPdf();
    http.expectNone(`${environment.apiUrl}/api/sorteo-transparencia/17/pdf`);
  });

  it('muestra el aviso estándar si falla la descarga', () => {
    http.expectOne(`${environment.apiUrl}/api/sorteo-transparencia/17`).flush({
      fechaProgramada: lottery.fecha,
      estado: 'publicado',
    });
    component.downloadPdf();
    http.expectOne(`${environment.apiUrl}/api/sorteo-transparencia/17/pdf`).flush(new Blob(['Error']), {
      status: 500,
      statusText: 'Error del servidor',
    });
    expect(component.downloading).toBeFalse();
    expect(notifications.show).toHaveBeenCalledWith(
      'Documento no disponible', 'No fue posible descargar el PDF de transparencia.', 'error');
  });

  it('impide descargar un PDF publicado cuando el sorteo se reprogramará', () => {
    http.expectOne(`${environment.apiUrl}/api/sorteo-transparencia/17`).flush({
      fechaProgramada: lottery.fecha,
      estado: 'publicado',
    });
    component.showDownload = false;
    component.downloadPdf();
    http.expectNone(`${environment.apiUrl}/api/sorteo-transparencia/17/pdf`);
    expect(component.downloading).toBeFalse();
  });

});
