export interface ReferralProgramSettings {
  id: number;
  isActive: boolean;
  depositRewardPercentage: number;
  maxRewardedDeposits: number;
  winnerCashRewardAmount: number;
  minimumConfirmedTickets: number;
  updatedAt: string;
  rowVersion: string;
}

export interface ReferralProgramBenefits {
  isActive: boolean;
  depositRewardPercentage: number;
  maxRewardedDeposits: number;
  winnerCashRewardAmount: number;
  minimumConfirmedTickets: number;
}

export interface UpdateReferralProgramSettingsRequest {
  isActive: boolean;
  depositRewardPercentage: number;
  maxRewardedDeposits: number;
  winnerCashRewardAmount: number;
  minimumConfirmedTickets: number;
  rowVersion: string;
}
