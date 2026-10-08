using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eLotto.Migrations
{
    /// <inheritdoc />
    public partial class inicio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BoletosConfirmados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SorteosId = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FolioCompra = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioIdConfirm = table.Column<int>(type: "int", nullable: false),
                    WhatsAppConfirm = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CuentaAsignada = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoletosConfirmados", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GanadoresSorteos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FolioSorteo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaFin = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WhatsAppGanador = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UsuarioIdGanador = table.Column<int>(type: "int", nullable: false),
                    NombreGanador = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GanadoresSorteos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Rol",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rol", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sorteos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Imagen1 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Imagen2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Imagen3 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UrlTransmisionEnVivo = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    PrecioBoleto = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PrecioPorMil = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CantidadBoletos = table.Column<int>(type: "int", nullable: false),
                    PorcentajeMinimoVenta = table.Column<int>(type: "int", nullable: false, defaultValue: 70),
                    RascaditosHabilitados = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    GanadoresPorGrupo = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    RascaditosPorGrupo = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ImporteDepositoStripePorRascadito = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    NumeroGanador = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UsuarioIdGanador = table.Column<int>(type: "int", nullable: false),
                    NombreGanador = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sorteos", x => x.Id);
                    table.CheckConstraint("CK_Sorteos_PorcentajeMinimoVenta", "[PorcentajeMinimoVenta] >= 1 AND [PorcentajeMinimoVenta] <= 99");
                    table.CheckConstraint("CK_Sorteos_RascaditosConfiguracion", "([RascaditosHabilitados] = 0 AND [GanadoresPorGrupo] = 0 AND [RascaditosPorGrupo] = 0 AND [ImporteDepositoStripePorRascadito] = 0) OR ([RascaditosHabilitados] = 1 AND [GanadoresPorGrupo] > 0 AND [RascaditosPorGrupo] > 0 AND [GanadoresPorGrupo] <= [RascaditosPorGrupo] AND [ImporteDepositoStripePorRascadito] > 0)");
                });

            migrationBuilder.CreateTable(
                name: "UserRols",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    RolId = table.Column<int>(type: "int", nullable: false),
                    Expire = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRols", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    WhatsApp = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    User = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Password = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ConfirmCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Languaje = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDark = table.Column<bool>(type: "bit", nullable: false),
                    IsAndroid = table.Column<bool>(type: "bit", nullable: false),
                    IsIos = table.Column<bool>(type: "bit", nullable: false),
                    StripeCustomerId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SorteosBoletos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SorteosId = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FolioCompra = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Pagado = table.Column<bool>(type: "bit", nullable: false),
                    PreApartado = table.Column<bool>(type: "bit", nullable: false),
                    Apartado = table.Column<bool>(type: "bit", nullable: false),
                    Aviso = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SorteosBoletos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SorteosBoletos_Sorteos_SorteosId",
                        column: x => x.SorteosId,
                        principalTable: "Sorteos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SorteosRascaditoPremios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SorteosId = table.Column<int>(type: "int", nullable: false),
                    Premio = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Cantidad = table.Column<int>(type: "int", nullable: false),
                    Entregados = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SorteosRascaditoPremios", x => x.Id);
                    table.CheckConstraint("CK_SorteosRascaditoPremios_Cantidad", "[Cantidad] > 0");
                    table.CheckConstraint("CK_SorteosRascaditoPremios_Entregados", "[Entregados] >= 0 AND [Entregados] <= [Cantidad]");
                    table.CheckConstraint("CK_SorteosRascaditoPremios_Premio", "[Premio] > 0");
                    table.ForeignKey(
                        name: "FK_SorteosRascaditoPremios_Sorteos_SorteosId",
                        column: x => x.SorteosId,
                        principalTable: "Sorteos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserWallets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserWallets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserWallets_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WalletTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    WalletId = table.Column<int>(type: "int", nullable: false),
                    SorteoId = table.Column<int>(type: "int", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StripePaymentIntentId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    StripeEventId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    StripeMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    UserMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WalletTransactions_Sorteos_SorteoId",
                        column: x => x.SorteoId,
                        principalTable: "Sorteos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WalletTransactions_UserWallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "UserWallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WalletTransactions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SorteosRascaditos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SorteosId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    Folio = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    WalletTransactionOrigenId = table.Column<int>(type: "int", nullable: false),
                    EsGanador = table.Column<bool>(type: "bit", nullable: false),
                    SorteosRascaditoPremioId = table.Column<int>(type: "int", nullable: true),
                    ImportePremio = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    WalletTransactionPremioId = table.Column<int>(type: "int", nullable: true),
                    MatrizResultado = table.Column<string>(type: "varchar(256)", unicode: false, maxLength: 256, nullable: true),
                    LineaGanadora = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: true),
                    Revelado = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    FechaGeneracion = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSDATETIME()"),
                    FechaRevelado = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SorteosRascaditos", x => x.Id);
                    table.CheckConstraint("CK_SorteosRascaditos_Acreditacion", "([EsGanador] = 0 AND [WalletTransactionPremioId] IS NULL) OR ([EsGanador] = 1 AND [Revelado] = 0 AND [WalletTransactionPremioId] IS NULL) OR ([EsGanador] = 1 AND [Revelado] = 1 AND [WalletTransactionPremioId] IS NOT NULL)");
                    table.CheckConstraint("CK_SorteosRascaditos_FechaRevelado", "[FechaRevelado] IS NULL OR [FechaRevelado] >= [FechaGeneracion]");
                    table.CheckConstraint("CK_SorteosRascaditos_Resultado", "([EsGanador] = 1 AND [SorteosRascaditoPremioId] IS NOT NULL AND [ImportePremio] IS NOT NULL AND [ImportePremio] > 0) OR ([EsGanador] = 0 AND [SorteosRascaditoPremioId] IS NULL AND [ImportePremio] IS NULL)");
                    table.CheckConstraint("CK_SorteosRascaditos_ResultadoVisual", "([MatrizResultado] IS NULL AND [LineaGanadora] IS NULL) OR ([MatrizResultado] IS NOT NULL AND (([EsGanador] = 1 AND [LineaGanadora] IS NOT NULL) OR ([EsGanador] = 0 AND [LineaGanadora] IS NULL)))");
                    table.CheckConstraint("CK_SorteosRascaditos_Revelado", "([Revelado] = 0 AND [FechaRevelado] IS NULL) OR ([Revelado] = 1 AND [FechaRevelado] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_SorteosRascaditos_SorteosRascaditoPremios_SorteosRascaditoPremioId",
                        column: x => x.SorteosRascaditoPremioId,
                        principalTable: "SorteosRascaditoPremios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SorteosRascaditos_Sorteos_SorteosId",
                        column: x => x.SorteosId,
                        principalTable: "Sorteos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SorteosRascaditos_Users_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SorteosRascaditos_WalletTransactions_WalletTransactionOrigenId",
                        column: x => x.WalletTransactionOrigenId,
                        principalTable: "WalletTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SorteosRascaditos_WalletTransactions_WalletTransactionPremioId",
                        column: x => x.WalletTransactionPremioId,
                        principalTable: "WalletTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SorteosBoletos_SorteosId_PreApartado_Fecha",
                table: "SorteosBoletos",
                columns: new[] { "SorteosId", "PreApartado", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_SorteosBoletos_SorteosId_UsuarioId_PreApartado",
                table: "SorteosBoletos",
                columns: new[] { "SorteosId", "UsuarioId", "PreApartado" });

            migrationBuilder.CreateIndex(
                name: "UX_SorteosBoletos_SorteosId_Numero",
                table: "SorteosBoletos",
                columns: new[] { "SorteosId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SorteosRascaditoPremios_SorteosId",
                table: "SorteosRascaditoPremios",
                column: "SorteosId");

            migrationBuilder.CreateIndex(
                name: "IX_SorteosRascaditos_SorteosId_EsGanador",
                table: "SorteosRascaditos",
                columns: new[] { "SorteosId", "EsGanador" });

            migrationBuilder.CreateIndex(
                name: "IX_SorteosRascaditos_SorteosId_UsuarioId_EsGanador",
                table: "SorteosRascaditos",
                columns: new[] { "SorteosId", "UsuarioId", "EsGanador" });

            migrationBuilder.CreateIndex(
                name: "IX_SorteosRascaditos_SorteosRascaditoPremioId",
                table: "SorteosRascaditos",
                column: "SorteosRascaditoPremioId");

            migrationBuilder.CreateIndex(
                name: "IX_SorteosRascaditos_WalletTransactionOrigenId",
                table: "SorteosRascaditos",
                column: "WalletTransactionOrigenId");

            migrationBuilder.CreateIndex(
                name: "UX_SorteosRascaditos_UsuarioId_SorteosId_Folio",
                table: "SorteosRascaditos",
                columns: new[] { "UsuarioId", "SorteosId", "Folio" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_SorteosRascaditos_WalletTransactionPremioId",
                table: "SorteosRascaditos",
                column: "WalletTransactionPremioId",
                unique: true,
                filter: "[WalletTransactionPremioId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_StripeCustomerId",
                table: "Users",
                column: "StripeCustomerId",
                unique: true,
                filter: "[StripeCustomerId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Users_WhatsApp",
                table: "Users",
                column: "WhatsApp",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserWallets_UserId",
                table: "UserWallets",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_SorteoId",
                table: "WalletTransactions",
                column: "SorteoId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_StripeEventId",
                table: "WalletTransactions",
                column: "StripeEventId",
                unique: true,
                filter: "[StripeEventId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_StripePaymentIntentId",
                table: "WalletTransactions",
                column: "StripePaymentIntentId",
                unique: true,
                filter: "[StripePaymentIntentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_UserId",
                table: "WalletTransactions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_WalletId",
                table: "WalletTransactions",
                column: "WalletId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BoletosConfirmados");

            migrationBuilder.DropTable(
                name: "GanadoresSorteos");

            migrationBuilder.DropTable(
                name: "Rol");

            migrationBuilder.DropTable(
                name: "SorteosBoletos");

            migrationBuilder.DropTable(
                name: "SorteosRascaditos");

            migrationBuilder.DropTable(
                name: "UserRols");

            migrationBuilder.DropTable(
                name: "SorteosRascaditoPremios");

            migrationBuilder.DropTable(
                name: "WalletTransactions");

            migrationBuilder.DropTable(
                name: "Sorteos");

            migrationBuilder.DropTable(
                name: "UserWallets");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
