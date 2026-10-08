import { ScratchcardSymbol } from '../scratchcards/scratchcard.models';

export type UserPrizeType = 'sorteo' | 'rascadito' | 'referido';

export interface UserPrizeHistoryItem {
  id: number;
  tipo: UserPrizeType;
  origen: 'sorteo' | 'vigente' | 'historico' | 'referido';
  fecha: string;
  sorteoId: number;
  sorteoNombre: string;
  sorteoImagen: string | null;
  numeroGanador: string | null;
  rascaditoId: number | null;
  rascaditoFolio: string | null;
  importePremio: number | null;
  matrizResultado: ScratchcardSymbol[] | null;
  lineaGanadora: string | null;
  referidoGanadorNombre: string | null;
  boletosRequeridos: number | null;
  boletosComprados: number | null;
}

export interface UserPrizeHistory {
  totalPremios: number;
  totalSorteos: number;
  totalRascaditos: number;
  totalReferidos: number;
  importeRascaditos: number;
  importeReferidos: number;
  premios: UserPrizeHistoryItem[];
}
