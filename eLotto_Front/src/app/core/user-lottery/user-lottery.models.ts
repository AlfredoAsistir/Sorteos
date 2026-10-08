export interface CurrentLottery {
  id: number;
  nombre: string;
  fecha: string;
  zonaHoraria: string;
  fechaInicioBloqueo: string;
  estado: 'disponible' | 'proximo_a_iniciar' | 'en_proceso' | 'finalizado';
  ventaDisponible: boolean;
  segundosParaInicio: number;
  segundosParaCierreVentas: number;
  urlTransmisionEnVivo: string | null;
  precioBoleto: number;
  precioPorMil: number;
  cantidadBoletos: number;
  boletosVendidos: number;
  porcentajeVenta: number;
  porcentajeMinimoVenta: number;
  ventaMinimaAlcanzada: boolean;
  rascaditosHabilitados: boolean;
  bolsaRascaditosDisponible: number;
  imagen1: string;
  imagen2: string | null;
  imagen3: string | null;
  imagen2Tema?: 'rascaditos' | 'referidos' | null;
  imagen3Tema?: 'rascaditos' | 'referidos' | null;
}

export interface TicketAvailability {
  numero: string;
  disponible: boolean;
  seleccionado?: boolean;
}

export interface ManualTicketQueryResponse {
  folioCompra: string;
  boletos: TicketAvailability[];
}

export interface PreReserveResponse {
  ok: boolean;
  folioCompra: string | null;
  expira: string | null;
  segundosParaExpirar: number;
  numeros: string[];
  noDisponibles: string[];
  mensaje: string;
}

export interface TicketPurchaseResponse {
  ok: boolean;
  codigo: 'purchase_completed' | 'tickets_unavailable' | 'insufficient_balance';
  mensaje: string;
  total: number;
  saldo: number;
  numeros: string[];
  noDisponibles: string[];
}

export interface CurrentUserTickets {
  sorteoId: number;
  nombreSorteo: string;
  fechaSorteo: string;
  compras: UserTicketPurchase[];
  reenvioDisponibleEnSegundos: number;
}

export interface UserTicketPurchase {
  folioCompra: string;
  fechaCompra: string;
  cantidadBoletos: number;
  precioBoleto: number;
  importe: number;
  numeros: string[];
}

export interface TicketWhatsAppResend {
  ok: boolean;
  mensaje: string;
  reintentarEnSegundos: number;
}

export type RandomTicketType = 'azar' | 'inicio' | 'fin' | 'incluya';
