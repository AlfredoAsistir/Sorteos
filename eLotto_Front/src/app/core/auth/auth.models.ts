export interface LoginRequest {
  username: string;
  password: string;
  device: string;
}

export interface LoginResponse {
  ok?: boolean;
  token?: string;
  requiresConfirmation?: boolean;
  confirmationSent?: boolean;
  maskedWhatsApp?: string;
  message?: string;
  expire?: string | number | null;
  languaje?: string | null;
  rolId?: string | number | null;
  rolName?: string | null;
  user?: string | null;
  userId?: string | number | null;
  isDark?: boolean;
}

export interface LoginConfirmationRequest extends LoginRequest {
  pin: string;
}

export interface RegisterRequest {
  user: string;
  name: string;
  password: string;
  confirmPassword: string;
  whatsApp: string;
  device: string;
  confirmedOver18: boolean;
  referralCode?: string;
}

export interface ResetPasswordRequest {
  whatsApp: string;
  password: string;
  confirmPassword: string;
  confirmCode: string;
}

export interface ApiOperationResponse {
  ok: boolean;
  message: string;
  confirmationSent?: boolean;
  welcomeSent?: boolean;
}

export interface AuthSession {
  token: string;
  expire?: string;
  languaje?: string;
  rolId?: string;
  rolName?: string;
  user?: string;
  userId?: string;
  isDark?: boolean;
  keepSession: boolean;
}
