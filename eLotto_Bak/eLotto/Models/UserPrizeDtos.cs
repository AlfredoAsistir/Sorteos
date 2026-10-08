namespace eLotto.Models
{
    public sealed record UserPrizeHistoryItemResponse(
        long Id,
        string Tipo,
        string Origen,
        DateTime Fecha,
        int SorteoId,
        string SorteoNombre,
        string SorteoImagen,
        string NumeroGanador,
        long? RascaditoId,
        string RascaditoFolio,
        decimal? ImportePremio,
        IReadOnlyList<string> MatrizResultado,
        string LineaGanadora,
        string ReferidoGanadorNombre,
        int? BoletosRequeridos,
        int? BoletosComprados);

    public sealed record UserPrizeHistoryResponse(
        int TotalPremios,
        int TotalSorteos,
        int TotalRascaditos,
        int TotalReferidos,
        decimal ImporteRascaditos,
        decimal ImporteReferidos,
        IReadOnlyList<UserPrizeHistoryItemResponse> Premios);
}
