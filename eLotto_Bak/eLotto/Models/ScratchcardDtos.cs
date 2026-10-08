namespace eLotto.Models
{
    public sealed record ScratchcardListItemResponse(
        int Id,
        string Folio,
        bool Revelado,
        DateTime FechaGeneracion,
        DateTime? FechaRevelado);

    public sealed record ScratchcardListResponse(
        int Pendientes,
        IReadOnlyList<ScratchcardListItemResponse> Rascaditos);

    public sealed record ScratchcardStartResponse(
        int Id,
        string Folio,
        decimal PremioPosible,
        IReadOnlyList<string> MatrizResultado);
    public sealed record ScratchcardRevealResponse(
        int Id,
        string Folio,
        bool Revelado,
        bool ReveladoAhora,
        bool EsGanador,
        decimal? ImportePremio,
        decimal PremioPosible,
        DateTime FechaRevelado,
        decimal SaldoActual,
        IReadOnlyList<string> MatrizResultado,
        string LineaGanadora);
}
