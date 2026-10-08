using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eLotto.Migrations
{
    /// <inheritdoc />
    public partial class FinalizacionSorteoHistoricos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NumeroGanador",
                table: "GanadoresSorteos",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SorteoId",
                table: "GanadoresSorteos",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "WhatsAppConfirm",
                table: "BoletosConfirmados",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Numero",
                table: "BoletosConfirmados",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FolioCompra",
                table: "BoletosConfirmados",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CuentaAsignada",
                table: "BoletosConfirmados",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "BoletosConfirmadosHistorial",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SorteoId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    FolioCompra = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    NumerosJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CantidadBoletos = table.Column<int>(type: "int", nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ImporteTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FechaCompra = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WhatsAppConfirm = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CuentaAsignada = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    SorteoNombre = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaSorteo = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaArchivado = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoletosConfirmadosHistorial", x => x.Id);
                    table.CheckConstraint("CK_BoletosConfirmadosHistorial_Cantidad", "[CantidadBoletos] > 0");
                    table.CheckConstraint("CK_BoletosConfirmadosHistorial_Importes", "[PrecioUnitario] >= 0 AND [ImporteTotal] >= 0");
                    table.CheckConstraint("CK_BoletosConfirmadosHistorial_NumerosJson", "ISJSON([NumerosJson]) = 1");
                    table.ForeignKey(
                        name: "FK_BoletosConfirmadosHistorial_Sorteos_SorteoId",
                        column: x => x.SorteoId,
                        principalTable: "Sorteos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BoletosConfirmadosHistorial_Users_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SorteosRascaditosGanadoresHistorial",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SorteoId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    Folio = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    ImportePremio = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MatrizResultado = table.Column<string>(type: "varchar(256)", unicode: false, maxLength: 256, nullable: false),
                    LineaGanadora = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    FechaGeneracion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaRevelado = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WalletTransactionOrigenId = table.Column<int>(type: "int", nullable: false),
                    WalletTransactionPremioId = table.Column<int>(type: "int", nullable: false),
                    SorteoNombre = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SorteoImagen = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    FechaArchivado = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SorteosRascaditosGanadoresHistorial", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SorteosRascaditosGanadoresHistorial_Sorteos_SorteoId",
                        column: x => x.SorteoId,
                        principalTable: "Sorteos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SorteosRascaditosGanadoresHistorial_Users_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SorteosRascaditosGanadoresHistorial_WalletTransactions_WalletTransactionOrigenId",
                        column: x => x.WalletTransactionOrigenId,
                        principalTable: "WalletTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SorteosRascaditosGanadoresHistorial_WalletTransactions_WalletTransactionPremioId",
                        column: x => x.WalletTransactionPremioId,
                        principalTable: "WalletTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GanadoresSorteos_SorteoId",
                table: "GanadoresSorteos",
                column: "SorteoId",
                unique: true,
                filter: "[SorteoId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BoletosConfirmados_SorteosId_Numero",
                table: "BoletosConfirmados",
                columns: new[] { "SorteosId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BoletosConfirmados_SorteosId_UsuarioId_FolioCompra",
                table: "BoletosConfirmados",
                columns: new[] { "SorteosId", "UsuarioId", "FolioCompra" });

            migrationBuilder.CreateIndex(
                name: "IX_BoletosConfirmados_UsuarioId",
                table: "BoletosConfirmados",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_BoletosConfirmadosHistorial_SorteoId_UsuarioId_FolioCompra",
                table: "BoletosConfirmadosHistorial",
                columns: new[] { "SorteoId", "UsuarioId", "FolioCompra" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BoletosConfirmadosHistorial_UsuarioId_FechaCompra",
                table: "BoletosConfirmadosHistorial",
                columns: new[] { "UsuarioId", "FechaCompra" });

            migrationBuilder.CreateIndex(
                name: "IX_SorteosRascaditosGanadoresHistorial_SorteoId",
                table: "SorteosRascaditosGanadoresHistorial",
                column: "SorteoId");

            migrationBuilder.CreateIndex(
                name: "IX_SorteosRascaditosGanadoresHistorial_UsuarioId_FechaRevelado",
                table: "SorteosRascaditosGanadoresHistorial",
                columns: new[] { "UsuarioId", "FechaRevelado" });

            migrationBuilder.CreateIndex(
                name: "IX_SorteosRascaditosGanadoresHistorial_UsuarioId_SorteoId_Folio",
                table: "SorteosRascaditosGanadoresHistorial",
                columns: new[] { "UsuarioId", "SorteoId", "Folio" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SorteosRascaditosGanadoresHistorial_WalletTransactionOrigenId",
                table: "SorteosRascaditosGanadoresHistorial",
                column: "WalletTransactionOrigenId");

            migrationBuilder.CreateIndex(
                name: "IX_SorteosRascaditosGanadoresHistorial_WalletTransactionPremioId",
                table: "SorteosRascaditosGanadoresHistorial",
                column: "WalletTransactionPremioId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_BoletosConfirmados_Sorteos_SorteosId",
                table: "BoletosConfirmados",
                column: "SorteosId",
                principalTable: "Sorteos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BoletosConfirmados_Users_UsuarioId",
                table: "BoletosConfirmados",
                column: "UsuarioId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GanadoresSorteos_Sorteos_SorteoId",
                table: "GanadoresSorteos",
                column: "SorteoId",
                principalTable: "Sorteos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BoletosConfirmados_Sorteos_SorteosId",
                table: "BoletosConfirmados");

            migrationBuilder.DropForeignKey(
                name: "FK_BoletosConfirmados_Users_UsuarioId",
                table: "BoletosConfirmados");

            migrationBuilder.DropForeignKey(
                name: "FK_GanadoresSorteos_Sorteos_SorteoId",
                table: "GanadoresSorteos");

            migrationBuilder.DropTable(
                name: "BoletosConfirmadosHistorial");

            migrationBuilder.DropTable(
                name: "SorteosRascaditosGanadoresHistorial");

            migrationBuilder.DropIndex(
                name: "IX_GanadoresSorteos_SorteoId",
                table: "GanadoresSorteos");

            migrationBuilder.DropIndex(
                name: "IX_BoletosConfirmados_SorteosId_Numero",
                table: "BoletosConfirmados");

            migrationBuilder.DropIndex(
                name: "IX_BoletosConfirmados_SorteosId_UsuarioId_FolioCompra",
                table: "BoletosConfirmados");

            migrationBuilder.DropIndex(
                name: "IX_BoletosConfirmados_UsuarioId",
                table: "BoletosConfirmados");

            migrationBuilder.DropColumn(
                name: "NumeroGanador",
                table: "GanadoresSorteos");

            migrationBuilder.DropColumn(
                name: "SorteoId",
                table: "GanadoresSorteos");

            migrationBuilder.AlterColumn<string>(
                name: "WhatsAppConfirm",
                table: "BoletosConfirmados",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Numero",
                table: "BoletosConfirmados",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "FolioCompra",
                table: "BoletosConfirmados",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldMaxLength: 40);

            migrationBuilder.AlterColumn<string>(
                name: "CuentaAsignada",
                table: "BoletosConfirmados",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(250)",
                oldMaxLength: 250,
                oldNullable: true);
        }
    }
}
