using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eLotto.Migrations
{
    /// <inheritdoc />
    public partial class Inicio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReferralProgramSettings",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DepositRewardPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    MaxRewardedDeposits = table.Column<int>(type: "int", nullable: false),
                    WinnerCashRewardAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MinimumConfirmedTickets = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferralProgramSettings", x => x.Id);
                    table.CheckConstraint("CK_ReferralProgramSettings_DepositRewardPercentage", "[DepositRewardPercentage] >= 0 AND [DepositRewardPercentage] <= 100");
                    table.CheckConstraint("CK_ReferralProgramSettings_MaxRewardedDeposits", "[MaxRewardedDeposits] >= 0");
                    table.CheckConstraint("CK_ReferralProgramSettings_MinimumConfirmedTickets", "[MinimumConfirmedTickets] >= 0");
                    table.CheckConstraint("CK_ReferralProgramSettings_Singleton", "[Id] = 1");
                    table.CheckConstraint("CK_ReferralProgramSettings_WinnerCashRewardAmount", "[WinnerCashRewardAmount] >= 0");
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
                    Imagen2Tema = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    Imagen3Tema = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ZonaHoraria = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, defaultValue: "CDMX"),
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
                    ActiveSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConfirmCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Over18ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Languaje = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDark = table.Column<bool>(type: "bit", nullable: false),
                    IsAndroid = table.Column<bool>(type: "bit", nullable: false),
                    IsIos = table.Column<bool>(type: "bit", nullable: false),
                    StripeCustomerId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ReferralCode = table.Column<string>(type: "varchar(8)", unicode: false, maxLength: 8, nullable: false),
                    ReferredByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.CheckConstraint("CK_Users_ReferralCode_Format", "DATALENGTH([ReferralCode]) = 8 AND [ReferralCode] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ]%'");
                    table.ForeignKey(
                        name: "FK_Users_Users_ReferredByUserId",
                        column: x => x.ReferredByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GanadoresSorteos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SorteoId = table.Column<int>(type: "int", nullable: true),
                    NumeroGanador = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
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
                    table.ForeignKey(
                        name: "FK_GanadoresSorteos_Sorteos_SorteoId",
                        column: x => x.SorteoId,
                        principalTable: "Sorteos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                name: "BoletosConfirmados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SorteosId = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FolioCompra = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioIdConfirm = table.Column<int>(type: "int", nullable: false),
                    WhatsAppConfirm = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CuentaAsignada = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoletosConfirmados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoletosConfirmados_Sorteos_SorteosId",
                        column: x => x.SorteosId,
                        principalTable: "Sorteos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BoletosConfirmados_Users_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

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
                    FechaArchivado = table.Column<DateTime>(type: "datetime2", nullable: false)
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
                name: "ReferralWinnerCashRewards",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WinnerRecordId = table.Column<int>(type: "int", nullable: false),
                    ReferrerUserId = table.Column<int>(type: "int", nullable: false),
                    RequiredTickets = table.Column<int>(type: "int", nullable: false),
                    ActualTickets = table.Column<int>(type: "int", nullable: false),
                    RewardAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferralWinnerCashRewards", x => x.Id);
                    table.CheckConstraint("CK_ReferralWinnerCashRewards_ActualTickets", "[ActualTickets] >= 0");
                    table.CheckConstraint("CK_ReferralWinnerCashRewards_PaymentState", "([Status] = 1 AND [PaidAt] IS NULL) OR ([Status] = 2 AND [PaidAt] IS NOT NULL)");
                    table.CheckConstraint("CK_ReferralWinnerCashRewards_RequiredTickets", "[RequiredTickets] >= 0");
                    table.CheckConstraint("CK_ReferralWinnerCashRewards_RewardAmount", "[RewardAmount] > 0");
                    table.CheckConstraint("CK_ReferralWinnerCashRewards_Status", "[Status] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_ReferralWinnerCashRewards_GanadoresSorteos_WinnerRecordId",
                        column: x => x.WinnerRecordId,
                        principalTable: "GanadoresSorteos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReferralWinnerCashRewards_Users_ReferrerUserId",
                        column: x => x.ReferrerUserId,
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
                name: "ReferralDepositRewards",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReferredUserId = table.Column<int>(type: "int", nullable: false),
                    ReferrerUserId = table.Column<int>(type: "int", nullable: false),
                    SourceDepositTransactionId = table.Column<int>(type: "int", nullable: false),
                    RewardWalletTransactionId = table.Column<int>(type: "int", nullable: false),
                    DepositAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PercentageApplied = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    RewardAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferralDepositRewards", x => x.Id);
                    table.CheckConstraint("CK_ReferralDepositRewards_DepositAmount", "[DepositAmount] > 0");
                    table.CheckConstraint("CK_ReferralDepositRewards_DifferentUsers", "[ReferredUserId] <> [ReferrerUserId]");
                    table.CheckConstraint("CK_ReferralDepositRewards_PercentageApplied", "[PercentageApplied] > 0 AND [PercentageApplied] <= 100");
                    table.CheckConstraint("CK_ReferralDepositRewards_RewardAmount", "[RewardAmount] > 0");
                    table.ForeignKey(
                        name: "FK_ReferralDepositRewards_Users_ReferredUserId",
                        column: x => x.ReferredUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReferralDepositRewards_Users_ReferrerUserId",
                        column: x => x.ReferrerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReferralDepositRewards_WalletTransactions_RewardWalletTransactionId",
                        column: x => x.RewardWalletTransactionId,
                        principalTable: "WalletTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReferralDepositRewards_WalletTransactions_SourceDepositTransactionId",
                        column: x => x.SourceDepositTransactionId,
                        principalTable: "WalletTransactions",
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
                    FechaGeneracion = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    FechaArchivado = table.Column<DateTime>(type: "datetime2", nullable: false)
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
                name: "IX_GanadoresSorteos_SorteoId",
                table: "GanadoresSorteos",
                column: "SorteoId",
                unique: true,
                filter: "[SorteoId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralDepositRewards_ReferredUserId_CreatedAt",
                table: "ReferralDepositRewards",
                columns: new[] { "ReferredUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReferralDepositRewards_ReferrerUserId_CreatedAt",
                table: "ReferralDepositRewards",
                columns: new[] { "ReferrerUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_ReferralDepositRewards_RewardWalletTransactionId",
                table: "ReferralDepositRewards",
                column: "RewardWalletTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ReferralDepositRewards_SourceDepositTransactionId",
                table: "ReferralDepositRewards",
                column: "SourceDepositTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReferralWinnerCashRewards_ReferrerUserId_CreatedAt",
                table: "ReferralWinnerCashRewards",
                columns: new[] { "ReferrerUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_ReferralWinnerCashRewards_WinnerRecordId",
                table: "ReferralWinnerCashRewards",
                column: "WinnerRecordId",
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_Users_ReferredByUserId",
                table: "Users",
                column: "ReferredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_StripeCustomerId",
                table: "Users",
                column: "StripeCustomerId",
                unique: true,
                filter: "[StripeCustomerId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Users_ReferralCode",
                table: "Users",
                column: "ReferralCode",
                unique: true);

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
                name: "BoletosConfirmadosHistorial");

            migrationBuilder.DropTable(
                name: "ReferralDepositRewards");

            migrationBuilder.DropTable(
                name: "ReferralProgramSettings");

            migrationBuilder.DropTable(
                name: "ReferralWinnerCashRewards");

            migrationBuilder.DropTable(
                name: "Rol");

            migrationBuilder.DropTable(
                name: "SorteosBoletos");

            migrationBuilder.DropTable(
                name: "SorteosRascaditos");

            migrationBuilder.DropTable(
                name: "SorteosRascaditosGanadoresHistorial");

            migrationBuilder.DropTable(
                name: "UserRols");

            migrationBuilder.DropTable(
                name: "GanadoresSorteos");

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
