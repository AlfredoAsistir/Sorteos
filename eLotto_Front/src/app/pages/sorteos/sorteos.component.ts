import { CurrencyPipe } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { AbstractControl, FormBuilder, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';
import { firstValueFrom, Observable } from 'rxjs';
import { finalize } from 'rxjs/operators';
import { MaterialModule } from '../../material.module';
import { DateTimeFieldComponent } from '../../components/date-time-field/date-time-field.component';
import { NumericInputComponent } from '../../components/numeric-input/numeric-input.component';
import { FormNavigationDirective } from '../../core/forms/form-navigation.directive';
import { NotificationService } from '../../core/notifications/notification.service';
import { APP_OPERATIONAL_CONFIG } from '../../config/operational.config';
import { Sorteo, SorteoRascaditoPremio } from '../../core/sorteos/sorteo.models';
import { LoadingService } from '../../services/loading.service';
import { SorteosService } from '../../services/sorteos.service';

const scratchcardRatioValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  if (!control.get('rascaditosHabilitados')?.value) return null;
  const winners = Number(control.get('ganadoresPorGrupo')?.value ?? 0);
  const scratchcards = Number(control.get('rascaditosPorGrupo')?.value ?? 0);
  return winners > scratchcards ? { scratchcardRatio: true } : null;
};

const prizeAvailabilityValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const quantity = Number(control.get('cantidad')?.value ?? 0);
  const delivered = Number(control.get('entregados')?.value ?? 0);
  return delivered > quantity ? { deliveredExceedsQuantity: true } : null;
};

const defaultScratchcardConfiguration = APP_OPERATIONAL_CONFIG.administration.scratchcards;
const defaultTimeZone = 'CDMX';
const mexicanTimeZones = [
  { id: defaultTimeZone, label: 'Ciudad de México' },
  { id: 'Cancun', label: 'Cancún · Quintana Roo' },
  { id: 'Merida', label: 'Mérida · Campeche y Yucatán' },
  { id: 'Monterrey', label: 'Monterrey · noreste' },
  { id: 'Matamoros', label: 'Matamoros · frontera noreste' },
  { id: 'Chihuahua', label: 'Chihuahua' },
  { id: 'Ciudad_Juarez', label: 'Ciudad Juárez · frontera oeste' },
  { id: 'Ojinaga', label: 'Ojinaga · frontera este' },
  { id: 'Mazatlan', label: 'Mazatlán · noroeste' },
  { id: 'Bahia_Banderas', label: 'Bahía de Banderas' },
  { id: 'Hermosillo', label: 'Hermosillo · Sonora' },
  { id: 'Tijuana', label: 'Tijuana · Baja California' },
] as const;
const mexicanTimeZoneValidator: ValidatorFn = control =>
  mexicanTimeZones.some(zone => zone.id === control.value) ? null : { mexicanTimeZone: true };
const imageThemeOptions = [
  { value: 'rascaditos', label: 'Rascaditos' },
  { value: 'referidos', label: 'Referidos' },
] as const;
const imageThemeValidator: ValidatorFn = control =>
  !control.value || imageThemeOptions.some(option => option.value === control.value)
    ? null : { imageTheme: true };
type ScratchcardPrizeForm = FormGroup<{
  id: FormControl<number>;
  premio: FormControl<number>;
  cantidad: FormControl<number>;
  entregados: FormControl<number>;
}>;

@Component({ selector: 'app-sorteos', standalone: true, imports: [CurrencyPipe, ReactiveFormsModule, MaterialModule, DateTimeFieldComponent, NumericInputComponent, FormNavigationDirective], templateUrl: './sorteos.component.html', styleUrl: './sorteos.component.scss' })
export class SorteosComponent implements OnInit {
  readonly form = this.fb.nonNullable.group({
    nombre: ['', [Validators.required, Validators.maxLength(150)]],
    fechaDate: this.fb.control<Date | null>(null, Validators.required),
    fechaTime: [String(APP_OPERATIONAL_CONFIG.administration.defaultDrawTime), [Validators.required, Validators.pattern(/^(?:[01]\d|2[0-3]):00$/)]],
    zonaHoraria: [defaultTimeZone, [Validators.required, mexicanTimeZoneValidator]],
    urlTransmisionEnVivo: ['', [Validators.maxLength(2048), Validators.pattern(/^https:\/\/.+/i)]],
    precioBoleto: [0, [Validators.required, Validators.min(0)]],
    precioPorMil: [0, [Validators.required, Validators.min(0)]],
    cantidadBoletos: [1, [Validators.required, Validators.min(1)]],
    porcentajeMinimoVenta: [Number(APP_OPERATIONAL_CONFIG.administration.minimumSalesPercentage), [Validators.required, Validators.min(1), Validators.max(99)]],
    rascaditosHabilitados: [false],
    ganadoresPorGrupo: [Number(defaultScratchcardConfiguration.winnersPerGroup), [Validators.required, Validators.min(1)]],
    rascaditosPorGrupo: [Number(defaultScratchcardConfiguration.scratchcardsPerGroup), [Validators.required, Validators.min(1)]],
    importeDepositoStripePorRascadito: [Number(defaultScratchcardConfiguration.stripeDepositAmount), [Validators.required, Validators.min(0.01)]],
    imagen2Tema: ['', imageThemeValidator],
    imagen3Tema: ['', imageThemeValidator],
    rascaditoPremios: this.fb.array([this.createPrizeForm()], [Validators.minLength(1)])
  }, { validators: scratchcardRatioValidator });
  current: Sorteo | null = null; page = 1; total = 0; saving = false;
  selectedTab = 0;
  showValidationErrors = false;
  readonly mexicanTimeZones = mexicanTimeZones;
  readonly imageThemeOptions = imageThemeOptions;
  readonly displayImageTheme = (value: string | null): string =>
    imageThemeOptions.find(option => option.value === value)?.label ?? value ?? '';
  imageThemeControl(index: number): FormControl<string> {
    return index === 1 ? this.form.controls.imagen2Tema : this.form.controls.imagen3Tema;
  }
  filteredImageThemes(index: number): typeof imageThemeOptions[number][] {
    const value = this.imageThemeControl(index).value.trim().toLocaleLowerCase('es');
    return imageThemeOptions.filter(option => !value || option.value.includes(value) || option.label.toLocaleLowerCase('es').includes(value));
  }
  imageThemeInvalid(index: number): boolean {
    const control = this.imageThemeControl(index);
    return control.invalid || (this.files[index] ? !control.value : !this.previews[index] && !!control.value);
  }
  readonly displayTimeZone = (id: string | null): string =>
    mexicanTimeZones.find(zone => zone.id === id)?.label ?? id ?? '';
  get filteredTimeZones(): typeof mexicanTimeZones[number][] {
    const value = this.form.controls.zonaHoraria.value.trim().toLocaleLowerCase('es');
    if (!value || mexicanTimeZones.some(zone => zone.id.toLowerCase() === value)) return [...mexicanTimeZones];
    return mexicanTimeZones.filter(zone =>
      zone.label.toLocaleLowerCase('es').includes(value) || zone.id.toLowerCase().includes(value));
  }
  files: Array<File | null> = [null, null, null]; previews: Array<string | null> = [null, null, null];
  constructor(private readonly fb: FormBuilder, readonly service: SorteosService, private readonly notifications: NotificationService, private readonly changeDetector: ChangeDetectorRef, private readonly loading: LoadingService) {}
  ngOnInit(): void {
    this.setScratchcardControlsEnabled(false, false);
    this.load(1);
  }
  load(page: number, afterLoad?: () => void): void {
    this.service.getPage(page).subscribe({
      next: result => {
        this.page = result.totalRecords ? result.page : 0;
        this.total = result.totalRecords;
        result.sorteo ? this.show(result.sorteo) : this.new();
        this.changeDetector.detectChanges();
        afterLoad?.();
      },
      error: () => this.notifications.show('Error', 'No fue posible cargar los sorteos.', 'error')
    });
  }
  show(sorteo: Sorteo): void {
    this.selectedTab = 0;
    this.showValidationErrors = false;
    this.current = sorteo;
    const dateParts = /^([0-9]{4})-([0-9]{2})-([0-9]{2})T([0-9]{2}):([0-9]{2})/.exec(sorteo.fecha);
    const date = dateParts
      ? new Date(Number(dateParts[1]), Number(dateParts[2]) - 1, Number(dateParts[3]))
      : null;
    const time = dateParts ? `${dateParts[4]}:${dateParts[5]}` : '';
    const scratchcardsEnabled = sorteo.rascaditosHabilitados && (sorteo.rascaditoPremios?.length ?? 0) > 0;
    this.rascaditoPremios.clear();
    this.form.reset({ nombre: sorteo.nombre, fechaDate: date, fechaTime: time, zonaHoraria: sorteo.zonaHoraria ?? defaultTimeZone, precioBoleto: sorteo.precioBoleto, precioPorMil: sorteo.precioPorMil, cantidadBoletos: sorteo.cantidadBoletos, porcentajeMinimoVenta: sorteo.porcentajeMinimoVenta, rascaditosHabilitados: scratchcardsEnabled, ganadoresPorGrupo: sorteo.ganadoresPorGrupo || defaultScratchcardConfiguration.winnersPerGroup, rascaditosPorGrupo: sorteo.rascaditosPorGrupo || defaultScratchcardConfiguration.scratchcardsPerGroup, importeDepositoStripePorRascadito: sorteo.importeDepositoStripePorRascadito || defaultScratchcardConfiguration.stripeDepositAmount, imagen2Tema: sorteo.imagen2Tema ?? '', imagen3Tema: sorteo.imagen3Tema ?? '', rascaditoPremios: [] });
    this.form.controls.urlTransmisionEnVivo.setValue(sorteo.urlTransmisionEnVivo ?? '', { emitEvent: false });
    (sorteo.rascaditoPremios ?? []).forEach(premio => this.rascaditoPremios.push(this.createPrizeForm(premio)));
    this.setScratchcardControlsEnabled(scratchcardsEnabled);
    this.files = [null,null,null]; this.previews = [sorteo.imagen1, sorteo.imagen2, sorteo.imagen3].map(path => this.service.imageUrl(path)); this.form.markAsPristine();
  }
  new(): void {
    this.selectedTab = 0;
    this.showValidationErrors = false;
    this.current = null;
    this.rascaditoPremios.clear();
    this.form.reset({ nombre: '', fechaDate: null, fechaTime: APP_OPERATIONAL_CONFIG.administration.defaultDrawTime, zonaHoraria: defaultTimeZone, precioBoleto: 0, precioPorMil: 0, cantidadBoletos: 1, porcentajeMinimoVenta: APP_OPERATIONAL_CONFIG.administration.minimumSalesPercentage, rascaditosHabilitados: false, ganadoresPorGrupo: defaultScratchcardConfiguration.winnersPerGroup, rascaditosPorGrupo: defaultScratchcardConfiguration.scratchcardsPerGroup, importeDepositoStripePorRascadito: defaultScratchcardConfiguration.stripeDepositAmount, imagen2Tema: '', imagen3Tema: '', rascaditoPremios: [] });
    this.service.getServerClock().subscribe({ next: clock => {
      if (this.current || this.form.controls.fechaDate.dirty) return;
      const date = clock.localDateTime;
      this.form.controls.fechaDate.setValue(new Date(Number(date.slice(0, 4)), Number(date.slice(5, 7)) - 1, Number(date.slice(8, 10))));
      this.form.controls.fechaDate.markAsPristine();
      this.changeDetector.detectChanges();
    }});
    this.form.controls.urlTransmisionEnVivo.setValue('', { emitEvent: false });
    this.setScratchcardControlsEnabled(false, false);
    this.files=[null,null,null]; this.previews=[null,null,null]; this.form.markAsPristine();
  }
  get rascaditoPremios() { return this.form.controls.rascaditoPremios; }
  get totalPremiosConfigurados(): number {
    return this.rascaditoPremios.controls.reduce((total, premio) => total + Number(premio.controls.cantidad.value || 0), 0);
  }
  get bolsaTotal(): number {
    return this.rascaditoPremios.controls.reduce((total, premio) => total + this.totalPremio(premio.getRawValue()), 0);
  }
  get generalInvalid(): boolean {
    return this.form.controls.nombre.invalid || this.form.controls.fechaDate.invalid ||
      this.form.controls.fechaTime.invalid || this.form.controls.zonaHoraria.invalid ||
      this.form.controls.urlTransmisionEnVivo.invalid;
  }
  get ticketSettingsInvalid(): boolean {
    return this.form.controls.precioBoleto.invalid || this.form.controls.precioPorMil.invalid ||
      this.form.controls.cantidadBoletos.invalid || this.form.controls.porcentajeMinimoVenta.invalid;
  }
  get scratchcardsInvalid(): boolean {
    return this.form.hasError('scratchcardRatio') || (this.form.controls.rascaditosHabilitados.value && (
      this.form.controls.ganadoresPorGrupo.invalid || this.form.controls.rascaditosPorGrupo.invalid ||
      this.form.controls.importeDepositoStripePorRascadito.invalid || this.rascaditoPremios.invalid));
  }
  totalPremio(premio: Pick<SorteoRascaditoPremio, 'premio' | 'cantidad'>): number {
    return Number(premio.premio || 0) * Number(premio.cantidad || 0);
  }
  onScratchcardToggle(enabled: boolean): void { this.setScratchcardControlsEnabled(enabled); }
  addPrize(markDirty = true): void {
    this.rascaditoPremios.push(this.createPrizeForm());
    if (markDirty) this.form.markAsDirty();
  }
  async removePrize(prizeForm: ScratchcardPrizeForm): Promise<void> {
    if (this.saving) return;
    const prize = prizeForm.getRawValue();
    if (prize.entregados > 0) {
      this.notifications.show('Premio protegido', 'No se puede eliminar una configuración que ya tiene premios entregados.', 'warning');
      return;
    }

    this.saving = true;
    try {
      const confirmed = await this.notifications.confirmDeletion('la fila de premio', '¿Eliminar premio?');
      if (!confirmed) return;

      const removeFromForm = (): boolean => {
        const currentIndex = this.rascaditoPremios.controls.findIndex(currentPrizeForm =>
          currentPrizeForm === prizeForm ||
          prize.id > 0 && currentPrizeForm.controls.id.value === prize.id
        );
        if (currentIndex < 0) return false;
        this.rascaditoPremios.removeAt(currentIndex);
        this.rascaditoPremios.updateValueAndValidity();
        return true;
      };

      if (this.current && prize.id > 0) {
        const sorteoId = this.current.id;
        await firstValueFrom(this.service.deleteScratchcardPrize(sorteoId, prize.id));
        removeFromForm();
        if (this.current?.id === sorteoId) {
          this.current = {
            ...this.current,
            rascaditoPremios: this.current.rascaditoPremios.filter(currentPrize => currentPrize.id !== prize.id)
          };
        }
        this.notifications.show('Operación correcta', 'El premio del rascadito fue eliminado.', 'success');
        return;
      }

      if (removeFromForm()) this.form.markAsDirty();
    } catch {
      this.notifications.show('Error', 'No fue posible eliminar el premio del rascadito.', 'error');
    } finally {
      this.saving = false;
      this.changeDetector.detectChanges();
    }
  }
  selectImage(index: number, event: Event): void { const file = (event.target as HTMLInputElement).files?.[0] ?? null; if (!file) return; if (!['image/jpeg','image/png','image/webp'].includes(file.type) || file.size > 5*1024*1024) { this.notifications.show('Imagen no válida', 'Selecciona una imagen JPG, PNG o WEBP de máximo 5 MB.', 'warning'); return; } this.files[index]=file; this.previews[index]=URL.createObjectURL(file); this.form.markAsDirty(); }
  async removeImage(index: number): Promise<void> {
    if (index === 0 || !this.previews[index]) return;

    const confirmed = await this.notifications.confirmDeletion(`la imagen opcional ${index + 1}`);
    if (!confirmed) return;

    const imageNumber = (index + 1) as 2 | 3;
    const persistedPath = imageNumber === 2 ? this.current?.imagen2 : this.current?.imagen3;

    if (this.current && persistedPath) {
      this.saving = true;
      try {
        await firstValueFrom(this.service.deleteOptionalImage(this.current.id, imageNumber));
        this.load(this.page, () =>
          this.notifications.show('Operación correcta', `La imagen opcional ${imageNumber} fue eliminada.`, 'success')
        );
      } catch {
        this.notifications.show('Error', 'No fue posible eliminar la imagen opcional.', 'error');
      } finally {
        this.saving = false;
      }
      return;
    }

    const preview = this.previews[index];
    if (preview?.startsWith('blob:')) URL.revokeObjectURL(preview);
    this.files = this.files.map((file, fileIndex) => fileIndex === index ? null : file);
    this.previews = this.previews.map((image, imageIndex) => imageIndex === index ? null : image);
    this.imageThemeControl(index).setValue('');
    this.notifications.show('Operación correcta', `La imagen opcional ${imageNumber} fue eliminada.`, 'success');
  }
  save(): void {
    if (this.form.invalid || (!this.current && !this.files[0]) || this.imageThemeInvalid(1) || this.imageThemeInvalid(2)) {
      this.form.markAllAsTouched();
      this.showValidationErrors = true;
      this.selectedTab = this.generalInvalid ? 0 : this.ticketSettingsInvalid ? 1 : this.scratchcardsInvalid ? 2 : 3;
      if (this.selectedTab === 3 && !this.current && !this.files[0])
        this.notifications.show('Imagen requerida', 'Selecciona la imagen principal.', 'warning');
      return;
    }
    const value=this.form.getRawValue(); const date=value.fechaDate!; 
    const data=new FormData(); data.append('id',String(this.current?.id ?? 0)); data.append('nombre',value.nombre.trim()); data.append('fecha',this.formatLocalDateTime(date, value.fechaTime)); data.append('precioBoleto',String(value.precioBoleto)); data.append('precioPorMil',String(value.precioPorMil)); data.append('cantidadBoletos',String(value.cantidadBoletos)); data.append('porcentajeMinimoVenta',String(value.porcentajeMinimoVenta)); data.append('rascaditosHabilitados', String(value.rascaditosHabilitados)); data.append('ganadoresPorGrupo', String(value.rascaditosHabilitados ? value.ganadoresPorGrupo : 0)); data.append('rascaditosPorGrupo', String(value.rascaditosHabilitados ? value.rascaditosPorGrupo : 0)); data.append('importeDepositoStripePorRascadito', String(value.rascaditosHabilitados ? value.importeDepositoStripePorRascadito : 0)); if(value.rascaditosHabilitados){ value.rascaditoPremios.forEach((premio,index)=>{ data.append(`rascaditoPremios[${index}].id`,String(premio.id)); data.append(`rascaditoPremios[${index}].premio`,String(premio.premio)); data.append(`rascaditoPremios[${index}].cantidad`,String(premio.cantidad)); data.append(`rascaditoPremios[${index}].entregados`,String(premio.entregados)); }); } this.files.forEach((file,index)=>{ if(file) data.append(`imagen${index+1}`,file); });
    data.append('urlTransmisionEnVivo', value.urlTransmisionEnVivo.trim());
    data.append('zonaHoraria', value.zonaHoraria);
    data.append('imagen2Tema', value.imagen2Tema);
    data.append('imagen3Tema', value.imagen3Tema);
    this.saving=true; this.loading.show(); const request: Observable<Sorteo | void> = this.current ? this.service.update(this.current.id,data) : this.service.create(data); request.pipe(finalize(()=>{ this.saving=false; this.loading.hide(); })).subscribe({ next:()=>{ this.notifications.show('Operación correcta', this.current?'Sorteo actualizado.':'Sorteo creado.', 'success'); this.load(this.current?this.page:1); }, error:()=>this.notifications.show('Error', 'No fue posible guardar el sorteo.', 'error') });
  }
  async delete(): Promise<void> {
    if (!this.current) return;
    const confirmed = await this.notifications.confirmDeletion(`el sorteo "${this.current.nombre}"`, '¿Eliminar sorteo?');
    if (!confirmed) return;

    this.service.delete(this.current.id).subscribe({
      next: () => {
        this.notifications.show('Operación correcta', 'Sorteo eliminado.', 'success');
        this.load(Math.min(this.page, this.total - 1) || 1);
      },
      error: () => this.notifications.show('Error', 'No fue posible eliminar el sorteo.', 'error')
    });
  }
  go(page:number):void { if(page>=1&&page<=this.total&&page!==this.page)this.load(page); }
  undo():void { this.current ? this.load(this.page) : this.total ? this.load(1) : this.new(); }
  get dirty():boolean { return this.form.dirty || this.files.some(Boolean); }
  private createPrizeForm(premio?: SorteoRascaditoPremio): ScratchcardPrizeForm {
    return this.fb.nonNullable.group({
      id: [premio?.id ?? 0],
      premio: [premio?.premio ?? 0, [Validators.required, Validators.min(0.01)]],
      cantidad: [premio?.cantidad ?? 1, [Validators.required, Validators.min(1)]],
      entregados: [{ value: premio?.entregados ?? 0, disabled: true }]
    }, { validators: prizeAvailabilityValidator });
  }
  private addDefaultPrizes(): void {
    defaultScratchcardConfiguration.prizes.forEach(prize => this.rascaditoPremios.push(this.createPrizeForm({
      id: 0,
      sorteosId: this.current?.id ?? 0,
      premio: prize.premio,
      cantidad: prize.cantidad,
      entregados: 0
    })));
  }
  private setScratchcardControlsEnabled(enabled: boolean, addDefaultPrize = true): void {
    const options = { emitEvent: false };
    if (enabled) {
      this.form.controls.ganadoresPorGrupo.enable(options);
      this.form.controls.rascaditosPorGrupo.enable(options);
      this.form.controls.importeDepositoStripePorRascadito.enable(options);
      this.rascaditoPremios.enable(options);
      this.rascaditoPremios.controls.forEach(premio => premio.controls.entregados.disable(options));
      if (addDefaultPrize && this.rascaditoPremios.length === 0) this.addDefaultPrizes();
    } else {
      this.form.controls.ganadoresPorGrupo.disable(options);
      this.form.controls.rascaditosPorGrupo.disable(options);
      this.form.controls.importeDepositoStripePorRascadito.disable(options);
      this.rascaditoPremios.disable(options);
    }
    this.form.updateValueAndValidity(options);
  }
private formatLocalDateTime(date: Date, time: string): string { return `${date.getFullYear()}-${String(date.getMonth()+1).padStart(2,'0')}-${String(date.getDate()).padStart(2,'0')}T${time}:00`; }
}
