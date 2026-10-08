export type ScratchcardSymbol =
  | 'money'
  | 'star'
  | 'gift'
  | 'trophy'
  | 'ticket'
  | 'crown';

export type ScratchcardAutoPlaySpeed = 0.5 | 1 | 1.5;

export interface ScratchcardListItem {
  id: number;
  folio: string;
  revelado: boolean;
  fechaGeneracion: string;
  fechaRevelado?: string | null;
}

export interface ScratchcardListResponse {
  pendientes: number;
  rascaditos: ScratchcardListItem[];
}

export interface ScratchcardStartResponse {
  id: number;
  folio: string;
  premioPosible: number;
  matrizResultado: ScratchcardSymbol[];
}
export interface ScratchcardRevealResponse {
  id: number;
  folio: string;
  revelado: boolean;
  reveladoAhora: boolean;
  esGanador: boolean;
  importePremio?: number | null;
  premioPosible: number;
  fechaRevelado: string;
  saldoActual: number;
  matrizResultado: ScratchcardSymbol[];
  lineaGanadora?: string | null;
}

export interface ScratchcardHistoricalResult {
  id: number;
  folio: string;
  importePremio: number;
  fechaRevelado: string;
  matrizResultado: ScratchcardSymbol[];
  lineaGanadora: string;
}
export interface ScratchcardDialogData {
  scratchcard: ScratchcardListItem;
  hasNext: boolean;
  autoPlay: boolean;
  autoPlaySpeed?: ScratchcardAutoPlaySpeed;
  historyMode?: boolean;
  historicalResult?: ScratchcardHistoricalResult;
}

export interface ScratchcardDialogResult {
  scratchcardId: number;
  revealed: boolean;
  revealedAt?: string;
  prizeAwarded?: number;
  openNext?: boolean;
  autoPlay?: boolean;
  autoPlaySpeed?: ScratchcardAutoPlaySpeed;
  historyMode?: boolean;
  historicalResult?: ScratchcardHistoricalResult;
}
