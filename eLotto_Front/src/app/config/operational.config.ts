export const APP_OPERATIONAL_CONFIG = {
  administration: {
    defaultDrawTime: '20:00',
    minimumSalesPercentage: 70,
    scratchcards: {
      winnersPerGroup: 1,
      scratchcardsPerGroup: 20,
      stripeDepositAmount: 30,
      prizes: [
        { premio: 50, cantidad: 70 },
        { premio: 100, cantidad: 60 },
        { premio: 200, cantidad: 30 },
        { premio: 300, cantidad: 15 },
      ],
    },
  },
  wallet: {
    fallbackSuggestedDepositAmount: 300,
    fallbackMinimumDepositAmount: 100,
    fallbackMaximumDepositAmount: 2000,
    quickDepositAmounts: [100, 300, 500, 1000, 1500, 2000],
    paymentStatusPollingIntervalMs: 2000,
    paymentStatusMaximumAttempts: 9,
  },
} as const;