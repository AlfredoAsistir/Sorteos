using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eLotto.Migrations
{
    /// <inheritdoc />
    public partial class ReferralProgramPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReferralCode",
                table: "Users",
                type: "varchar(8)",
                unicode: false,
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReferredByUserId",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                """
                WHILE EXISTS (SELECT 1 FROM [Users] WHERE [ReferralCode] IS NULL)
                BEGIN
                    DECLARE @ReferralCode varchar(8) = '';

                    WHILE DATALENGTH(@ReferralCode) < 8
                    BEGIN
                        SET @ReferralCode = @ReferralCode + SUBSTRING(
                            'ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789',
                            (CONVERT(int, CRYPT_GEN_RANDOM(1)) % 36) + 1,
                            1);
                    END;

                    IF NOT EXISTS (SELECT 1 FROM [Users] WHERE [ReferralCode] = @ReferralCode)
                    BEGIN
                        UPDATE TOP (1) [Users]
                        SET [ReferralCode] = @ReferralCode
                        WHERE [ReferralCode] IS NULL;
                    END;
                END;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "ReferralCode",
                table: "Users",
                type: "varchar(8)",
                unicode: false,
                maxLength: 8,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(8)",
                oldUnicode: false,
                oldMaxLength: 8,
                oldNullable: true);

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
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSDATETIME()")
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
                name: "ReferralProgramSettings",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DepositRewardPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    MaxRewardedDeposits = table.Column<int>(type: "int", nullable: false),
                    WinnerCashRewardAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MinimumConfirmedTickets = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSDATETIME()"),
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

            migrationBuilder.Sql(
                """
                INSERT INTO [ReferralProgramSettings]
                    ([Id], [IsActive], [DepositRewardPercentage], [MaxRewardedDeposits],
                     [WinnerCashRewardAmount], [MinimumConfirmedTickets], [UpdatedAt])
                VALUES
                    (1, 1, 10.00, 5, 5000.00, 100, SYSDATETIME());
                """);

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
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSDATETIME()"),
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

            migrationBuilder.CreateIndex(
                name: "IX_Users_ReferredByUserId",
                table: "Users",
                column: "ReferredByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_Users_ReferralCode",
                table: "Users",
                column: "ReferralCode",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_ReferralCode_Format",
                table: "Users",
                sql: "DATALENGTH([ReferralCode]) = 8 AND [ReferralCode] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ]%'");

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

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Users_ReferredByUserId",
                table: "Users",
                column: "ReferredByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Users_ReferredByUserId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "ReferralDepositRewards");

            migrationBuilder.DropTable(
                name: "ReferralProgramSettings");

            migrationBuilder.DropTable(
                name: "ReferralWinnerCashRewards");

            migrationBuilder.DropIndex(
                name: "IX_Users_ReferredByUserId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "UX_Users_ReferralCode",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_ReferralCode_Format",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ReferralCode",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ReferredByUserId",
                table: "Users");
        }
    }
}
