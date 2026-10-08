export interface Sorteo {
  id: number;
  nombre: string;
  imagen1: string;
  imagen2: string | null;
  imagen3: string | null;
  imagen2Tema?: 'rascaditos' | 'referidos' | null;
  imagen3Tema?: 'rascaditos' | 'referidos' | null;
  fecha: string;
  zonaHoraria: string;
  urlTransmisionEnVivo: string | null;
  precioBoleto: number;
  precioPorMil: number;
  cantidadBoletos: number;
  porcentajeMinimoVenta: number;
  rascaditosHabilitados: boolean;
  ganadoresPorGrupo: number;
  rascaditosPorGrupo: number;
  importeDepositoStripePorRascadito: number;
  numeroGanador?: string | null;
  usuarioIdGanador?: number | null;
  nombreGanador?: string | null;
  rascaditoPremios: SorteoRascaditoPremio[];
}

export interface SorteoRascaditoPremio {
  id: number;
  sorteosId: number;
  premio: number;
  cantidad: number;
  entregados: number;
}

export interface SorteoPage {
  sorteo: Sorteo | null;
  totalRecords: number;
  page: number;
}

export interface SorteoWinnerVerification {
  sorteoId: number;
  numeroGanador: string;
  hayGanador: boolean;
  usuarioIdGanador: number | null;
  usuarioGanador: string | null;
  whatsAppGanador: string | null;
  folioCompra: string | null;
  referencia: WinnerReferralDetails | null;
}

export interface WinnerReferralDetails {
  referidorNombre: string;
  finalizado: boolean;
  premioGenerado: boolean | null;
  boletosRequeridos: number | null;
  boletosConfirmados: number | null;
  importePremio: number | null;
  estado: 1 | 2 | null;
  fechaPago: string | null;
}

export interface FinalizedLotteryWinner {
  sorteoId: number;
  sorteoNombre: string;
  numeroGanador: string;
  usuarioGanador: string;
  folioCompra: string;
  fechaFinalizacion: string;
  referencia: WinnerReferralDetails | null;
}

export interface SorteoRescheduleResult {
  sorteoId: number;
  numeroGanador: string | null;
  nuevaFecha: string;
  sorteosReprogramados: number;
}

export interface SorteoFinalizationResult {
  sorteoId: number;
  numeroGanador: string;
  comprasArchivadas: number;
  rascaditosGanadoresArchivados: number;
  rascaditosCaducados: number;
  ganador: FinalizedLotteryWinner;
}

