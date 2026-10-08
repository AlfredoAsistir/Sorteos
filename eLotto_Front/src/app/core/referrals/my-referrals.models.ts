export type ReferralWinnerCashRewardStatus = 1 | 2;

export interface DirectReferral {
  name: string;
  registeredAt: string;
  depositRewardsCount: number;
  depositRewardsTotal: number;
  winnerCashRewardsCount: number;
}

export interface ReferralDepositRewardHistory {
  referredUserName: string;
  createdAt: string;
  depositAmount: number;
  percentageApplied: number;
  rewardAmount: number;
}

export interface ReferralWinnerCashRewardHistory {
  winnerUserName: string;
  lotteryName: string;
  createdAt: string;
  requiredTickets: number;
  actualTickets: number;
  rewardAmount: number;
  status: ReferralWinnerCashRewardStatus;
  paidAt: string | null;
}

export interface MyReferrals {
  directReferralsCount: number;
  depositRewardsTotal: number;
  pendingCashRewardsTotal: number;
  paidCashRewardsTotal: number;
  referrals: DirectReferral[];
  depositRewards: ReferralDepositRewardHistory[];
  cashRewards: ReferralWinnerCashRewardHistory[];
}
