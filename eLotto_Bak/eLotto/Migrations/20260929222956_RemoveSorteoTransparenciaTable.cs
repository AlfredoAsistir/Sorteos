using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eLotto.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSorteoTransparenciaTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SorteoTransparencia");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SorteoTransparencia",
                columns: table => new
                {
                    SorteoId = table.Column<int>(type: "int", nullable: false),
                    FechaProgramada = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BoletosVendidos = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: false),
                    FechaCierre = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FechaPublicacion = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    HashLista = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: true),
                    HashPdf = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: true),
                    NumerosNoVendidosJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Pdf = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    PorcentajeMinimo = table.Column<int>(type: "int", nullable: false),
                    TotalBoletos = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SorteoTransparencia", x => new { x.SorteoId, x.FechaProgramada });
                    table.CheckConstraint("CK_SorteoTransparencia_Cantidades", "[TotalBoletos] > 0 AND [BoletosVendidos] >= 0 AND ([BoletosVendidos] <= [TotalBoletos] OR [Estado] = 'incidencia')");
                    table.CheckConstraint("CK_SorteoTransparencia_Estado", "[Estado] IN ('preparando', 'publicado', 'reprogramar', 'incidencia')");
                    table.ForeignKey(
                        name: "FK_SorteoTransparencia_Sorteos_SorteoId",
                        column: x => x.SorteoId,
                        principalTable: "Sorteos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });
            migrationBuilder.Sql("""
                CREATE TRIGGER dbo.TR_SorteoTransparencia_Immutable
                ON dbo.SorteoTransparencia AFTER UPDATE, DELETE AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted WHERE Estado = 'publicado')
                       OR EXISTS (
                           SELECT 1 FROM deleted d INNER JOIN inserted i
                             ON i.SorteoId = d.SorteoId AND i.FechaProgramada = d.FechaProgramada
                           WHERE i.FechaCierre <> d.FechaCierre OR i.TotalBoletos <> d.TotalBoletos
                              OR i.BoletosVendidos <> d.BoletosVendidos OR i.PorcentajeMinimo <> d.PorcentajeMinimo
                              OR ISNULL(i.NumerosNoVendidosJson, '') <> ISNULL(d.NumerosNoVendidosJson, '')
                              OR ISNULL(i.HashLista, '') <> ISNULL(d.HashLista, '')
                       )
                    BEGIN
                        ROLLBACK TRANSACTION;
                        THROW 51020, 'El documento de transparencia es inmutable.', 1;
                    END;
                END;
                """);
        }
    }
}
