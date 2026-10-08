using System.Buffers.Binary;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using eLotto.Controllers.api;
using eLotto.Core.Data;
using eLotto.Core.Models;
using eLotto.Core.Repository;
using eLotto.Core.Services;
using eLotto.Models;
using eLotto.Options;
using eLotto.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Stripe;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

var tests = new (string Name, Func<Task> Run)[]
{
    ("Horario de sorteo usa el mismo instante y la diferencia respecto al servidor", TestSorteoTimeConversionAsync),
    ("Horario de sorteo respeta los cambios estacionales de la zona", TestSorteoTimeSeasonalOffsetsAsync),
    ("Los nombres cortos del catálogo conservan las reglas de cada zona", TestCompactSorteoTimeZonesAsync),
    ("Fecha programada rechaza horas inexistentes, repetidas y zonas inválidas", TestSorteoScheduledTimeAsync),
    ("Consulta y compra validan boletos existentes sin límite por cantidad", TestStoredTicketNumbersAsync),
    ("El sorteo bloquea ventas exactamente 90 minutos antes", TestLotteryAvailabilityPolicyAsync),
    ("El sorteo vigente se ordena por instante entre zonas distintas", TestCurrentLotteryAcrossTimeZonesAsync),
    ("El cierre SQL respeta la zona del sorteo al preapartar", TestSalesGuardAcrossTimeZonesAsync),
    ("Preapartado y compra comparten siete minutos exactos", TestSevenMinuteReservationAsync),
    ("La compra guarda boletos y movimiento en la zona del sorteo", TestPurchaseUsesLotteryTimeZoneAsync),
    ("Stripe fecha el depósito tardío con la zona del sorteo", TestStripeDepositTimeZoneAsync),
    ("Stripe acredita solo depósitos con el nombre configurado de la aplicación", TestStripeApplicationMetadataAsync),
    ("La primera solicitud elegible publica un solo PDF físico para todos", TestTransparencyPublicationAsync),
    ("Verificar el resultado publica el PDF faltante y permite finalizar", TestResultPublishesTransparencyAsync),
    ("Venta insuficiente, datos inválidos y venta total determinan si se genera PDF", TestTransparencyFailClosedAsync),
    ("El estado y el PDF de transparencia son públicos", TestTransparencyEndpointAuthorizationAsync),
    ("La ruta física de transparencia queda fuera del directorio público", TestTransparencyStoragePathAsync),
    ("La lista de transparencia ordena números y rechaza ventas inconsistentes", TestTransparencyNumbersAsync),
    ("El PDF de transparencia admite miles de números en varias páginas", TestTransparencyLargePdfAsync),
    ("El PDF muestra cierre y emisión en la zona del sorteo sin sufijos de UTC", TestTransparencyPdfLotteryTimeAsync),
    ("Sin ganador reprograma siete días y conserva boletos vendidos", TestLotteryRescheduleWithoutWinnerAsync),
    ("Venta mínima no alcanzada impide finalizar y permite reprogramar", TestLotteryMinimumSaleRescheduleAsync),
    ("Finalizar archiva compras y ganadores, y caduca rascaditos pendientes", TestLotteryFinalizationAsync),
    ("Ganadores e historial se ordenan por instante entre zonas", TestWinnerHistoryAcrossTimeZonesAsync),
    ("El cálculo por depósito siempre trunca", TestDepositCalculationAsync),
    ("La capacidad guía la probabilidad sin limitar la generación", TestCapacityAsync),
    ("La probabilidad corrige proporción y dispersa usuarios", TestWinnerProbabilityAsync),
    ("Los premios se seleccionan por inventario restante", TestWeightedPrizeSelectionAsync),
    ("Los últimos dos depósitos Stripe definen el rango del premio", TestPrizeEligibilityPolicyAsync),
    ("El depósito anterior se ordena por instante entre zonas distintas", TestPrizeEligibilityAcrossTimeZonesAsync),
    ("La asignación real respeta rango, visual perdedora e inmutabilidad", TestPrizeEligibilityIntegrationAsync),
    ("La matriz ganadora siempre contiene una línea válida", TestWinningVisualMatricesAsync),
    ("La matriz perdedora nunca contiene líneas accidentales", TestLosingVisualMatricesAsync),
    ("El listado de usuario oculta resultados y prioriza pendientes", TestSafeScratchcardListAsync),
    ("He Ganado unifica sorteos y rascaditos sin exponer otros usuarios", TestUserPrizeHistoryAsync),
    ("La inicialización masiva genera 600000 boletos una sola vez", TestLargeTicketInitializationAsync),
    ("El modelo SQL protege resultado, folio e inventario concurrente", TestDatabaseInvariantsAsync),
    ("El webhook confirmado genera un lote atómico e idempotente", TestStripeIdempotencyAsync),
    ("El lote es idempotente por WalletTransactionOrigen", TestOriginBatchIdempotencyAsync),
    ("Eventos no confirmados y configuraciones inactivas no generan", TestNonGeneratingDepositsAsync),
    ("La configuración dinámica controla cálculo, premios e inventario", TestDynamicConfigurationAsync),
    ("Depósitos concurrentes superan la meta estratégica sin exceder los premios", TestConcurrentDepositCapacityAsync),
    ("El inventario agotado detiene rascaditos sin bloquear el depósito", TestExhaustedScratchcardInventoryAsync),
    ("La simulación estadística conserva proporción y dispersión", TestStatisticalDistributionAsync),
    ("Iniciar el raspado prepara la vista sin revelar ni acreditar", TestStartScratchcardAsync),
    ("Revelar un perdedor no modifica cartera ni inventario", TestRevealLosingScratchcardAsync),
    ("Revelar un ganador acredita una sola vez", TestRevealWinningScratchcardAsync),
    ("Dos revelados concurrentes producen un solo abono", TestConcurrentRevealAsync),
    ("Propiedad, inexistencia y datos corruptos no producen abonos", TestRevealSecurityAndCorruptionAsync),
    ("Inventario inconsistente bloquea cualquier abono", TestRevealInventoryIntegrityAsync),
    ("Cerrar una sesión anterior no revoca un login nuevo", TestSessionLogoutDoesNotRevokeNewLoginAsync),
    ("Los temas de imágenes secundarias se guardan y limpian al eliminar la imagen", TestOptionalImageTopicsAsync),
    ("La API rechaza temas inválidos o imágenes nuevas sin tema", TestOptionalImageTopicValidationAsync),
    ("Recuperar password confirma una cuenta pendiente solo con el PIN correcto", TestPendingAccountPasswordResetAsync),
    ("Los códigos propios usan ocho caracteres A-Z0-9", TestReferralCodeGenerationAsync),
    ("Una colisión de código se reintenta sin duplicar usuarios", TestReferralCodeCollisionRetryAsync),
    ("La migración de referidos protege backfill, esquema y reversión", TestReferralMigrationAsync),
    ("Ganador y recompensa se guardan juntos mediante la relación EF", TestReferralWinnerRelationshipAsync),
    ("El registro normal conserva atribución nula y genera código propio", TestNormalRegistrationReferralAsync),
    ("El registro exige y conserva la confirmación de mayoría de edad", TestAdultConfirmationRegistrationAsync),
    ("El registro referido normaliza y resuelve códigos válidos", TestReferredRegistrationNormalizationAsync),
    ("El registro rechaza códigos inválidos o inexistentes", TestInvalidReferralRegistrationAsync),
    ("La atribución funciona aunque el programa económico esté inactivo", TestInactiveReferralProgramAttributionAsync),
    ("La atribución conserva únicamente la relación directa", TestDirectReferralAttributionAsync),
    ("Un rol inexistente no deja un usuario referido parcial", TestReferralRegistrationAtomicityAsync),
    ("El request de registro no expone el Id del referidor", TestReferralAttributionImmutabilityAsync),
    ("La configuración de referidos valida ceros, rangos y precisión", TestReferralProgramSettingsValidationAsync),
    ("La configuración de referidos detecta actualizaciones concurrentes", TestReferralProgramSettingsConcurrencyAsync),
    ("La API de configuración está restringida al rol Admin", TestReferralProgramSettingsAuthorizationAsync),
    ("La invitación devuelve únicamente el código propio al usuario autenticado", TestReferralInvitationAsync),
    ("Un depósito sin referidor no genera bono", TestDepositWithoutReferrerRewardAsync),
    ("El depósito referido acredita wallet, movimiento e histórico auditable", TestReferralDepositRewardAsync),
    ("Las reglas deshabilitadas y el redondeo cero no consumen bonos", TestDisabledReferralDepositRewardsAsync),
    ("El máximo usa solamente bonos otorgados y respeta cambios posteriores", TestReferralDepositRewardLimitChangesAsync),
    ("El límite de bonos es independiente por cada usuario referido", TestIndependentReferralDepositLimitsAsync),
    ("El último bono disponible es concurrente e idempotente", TestConcurrentReferralDepositLimitAsync),
    ("La configuración ausente omite el bono y un fallo técnico revierte todo", TestReferralDepositFailureBehaviorAsync),
    ("El referidor participante recibe un premio en efectivo Pagado auditable", TestReferralWinnerCashRewardAsync),
    ("El premio del ganador respeta actividad, monto y boletos del mismo sorteo", TestReferralWinnerCashRewardEligibilityAsync),
    ("Una configuración ausente revierte la finalización hasta poder reintentarla", TestReferralWinnerCashRewardRollbackAsync),
    ("Mis referidos devuelve vacíos y usa exclusivamente el usuario del JWT", TestMyReferralsEmptyAndSecurityAsync),
    ("Mis referidos limita el primer nivel y proyecta históricos sin recalcular", TestMyReferralsHistoryAsync),
    ("El flujo integral de referidos conecta registro, depósito, ganador y consulta", TestReferralEndToEndFlowAsync),
};

var failures = new List<string>();
foreach (var test in tests.Where(test => args.Length == 0 || test.Name.Contains(args[0], StringComparison.OrdinalIgnoreCase)))
{
    try
    {
        await test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failures.Add($"{test.Name}: {ex.Message}");
        Console.WriteLine($"FAIL {test.Name}: {ex}");
    }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine($"{failures.Count} prueba(s) fallaron.");
    return 1;
}

Console.WriteLine($"{tests.Length} pruebas ejecutadas correctamente.");
return 0;

static Task TestSorteoTimeConversionAsync()
{
    ISorteoTimeService service = new SorteoTimeService();
    var serverMoment = new DateTimeOffset(2026, 9, 29, 10, 48, 0, TimeSpan.FromHours(-7));
    var sameZone = new Sorteos { ZonaHoraria = "Hermosillo" };
    var mexicoCity = new Sorteos { ZonaHoraria = "CDMX" };

    Equal(serverMoment, service.ConvertToSorteoTime(serverMoment, sameZone));
    Equal(TimeSpan.Zero, service.GetOffsetDifference(serverMoment, sameZone));

    var converted = service.ConvertToSorteoTime(serverMoment, mexicoCity);
    Equal(new DateTimeOffset(2026, 9, 29, 11, 48, 0, TimeSpan.FromHours(-6)), converted);
    Equal(TimeSpan.FromHours(-6), converted.Offset);
    Equal(TimeSpan.FromHours(1), service.GetOffsetDifference(serverMoment, mexicoCity));
    Equal(serverMoment.UtcDateTime, converted.UtcDateTime);
    Equal(TimeSpan.FromMinutes(5), serverMoment - service.ResolveRecordedTime(
        service.ConvertToSorteoTime(serverMoment.AddMinutes(-5), mexicoCity).DateTime,
        mexicoCity));

    var current = service.GetCurrent(mexicoCity);
    Equal(current.ServerNow.UtcDateTime, current.SorteoNow.UtcDateTime);
    Equal(current.SorteoNow.Offset - current.ServerNow.Offset, current.OffsetDifference);
    return Task.CompletedTask;
}

static Task TestSorteoTimeSeasonalOffsetsAsync()
{
    ISorteoTimeService service = new SorteoTimeService();
    var tijuana = new Sorteos { ZonaHoraria = "Tijuana" };
    var winter = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.FromHours(-7));
    var summer = new DateTimeOffset(2026, 7, 15, 10, 0, 0, TimeSpan.FromHours(-7));

    Equal(TimeSpan.FromHours(-1), service.GetOffsetDifference(winter, tijuana));
    Equal(TimeSpan.Zero, service.GetOffsetDifference(summer, tijuana));
    Equal(TimeSpan.FromHours(-8), service.ConvertToSorteoTime(winter, tijuana).Offset);
    Equal(TimeSpan.FromHours(-7), service.ConvertToSorteoTime(summer, tijuana).Offset);
    return Task.CompletedTask;
}

static Task TestCompactSorteoTimeZonesAsync()
{
    ISorteoTimeService service = new SorteoTimeService();
    var scheduled = new DateTime(2026, 2, 15, 12, 0, 0);
    var zones = new[]
    {
        "CDMX", "Cancun", "Merida", "Monterrey", "Matamoros", "Chihuahua",
        "Ciudad_Juarez", "Ojinaga", "Mazatlan", "Bahia_Banderas", "Hermosillo", "Tijuana"
    };

    foreach (var zone in zones)
    {
        var canonical = zone == "CDMX" ? "America/Mexico_City" : $"America/{zone}";
        Equal(
            service.ResolveScheduledTime(new Sorteos { Fecha = scheduled, ZonaHoraria = canonical }),
            service.ResolveScheduledTime(new Sorteos { Fecha = scheduled, ZonaHoraria = zone }));
    }

    return Task.CompletedTask;
}

static async Task TestSorteoScheduledTimeAsync()
{
    ISorteoTimeService service = new SorteoTimeService();
    var mexicoCity = new Sorteos
    {
        ZonaHoraria = "CDMX",
        Fecha = new DateTime(2026, 9, 29, 20, 0, 0)
    };
    Equal(new DateTimeOffset(2026, 9, 29, 20, 0, 0, TimeSpan.FromHours(-6)),
        service.ResolveScheduledTime(mexicoCity));

    var tijuana = new Sorteos { ZonaHoraria = "Tijuana" };
    tijuana.Fecha = new DateTime(2026, 3, 8, 2, 30, 0);
    await ThrowsAsync<ArgumentException>(() => Task.FromResult(service.ResolveScheduledTime(tijuana)));

    tijuana.Fecha = new DateTime(2026, 11, 1, 1, 30, 0);
    await ThrowsAsync<ArgumentException>(() => Task.FromResult(service.ResolveScheduledTime(tijuana)));
    Equal(new DateTimeOffset(2026, 11, 1, 1, 30, 0, TimeSpan.FromHours(-8)),
        service.ResolveRecordedTime(tijuana.Fecha, tijuana));

    tijuana.ZonaHoraria = "";
    await ThrowsAsync<ArgumentException>(() => Task.FromResult(service.ResolveScheduledTime(tijuana)));
}

static async Task TestStoredTicketNumbersAsync()
{
    foreach (var random in new[] { false, true })
    {
        var options = CreateOptions($"eLotto_StoredTicketNumbers_{Guid.NewGuid():N}");
        try
        {
            var seeded = await RecreateAndSeedAsync(options, 1, initialBalance: 10m);
            await using var context = new eLottoContext(options);
            var lottery = await context.Sorteos.SingleAsync(x => x.Id == seeded.SorteoId);
            lottery.CantidadBoletos = 60000;
            foreach (var number in new[] { "59999", "60000", "60001" })
                context.SorteosBoletos.Add(new SorteosBoletos
                {
                    SorteosId = lottery.Id,
                    Numero = number,
                    FolioCompra = string.Empty,
                    Fecha = ApplicationClock.Now
                });
            await context.SaveChangesAsync();

            var service = new UserLotteryService(
                new UserLotteryRepository(context), null,
                new NullConfirmedTicketDeliveryService(), null,
                NullLogger<UserLotteryService>.Instance,
                Options.Create(new LotteryRulesOptions()), new SorteoTimeService());
            var token = CancellationToken.None;
            var query = await service.QueryManualAsync(seeded.UserId,
                new ManualTicketQueryDto { Numeros = "59999-60001,60000,60002" }, token);
            Equal(4, query.Boletos.Count);
            True(query.Boletos.Where(x => x.Numero != "60002").All(x => x.Disponible),
                "Los boletos existentes, incluso superiores a la cantidad, deben estar disponibles.");
            True(!query.Boletos.Single(x => x.Numero == "60002").Disponible,
                "Un número inexistente no debe estar disponible.");

            var missing = await service.PreReserveManualAsync(seeded.UserId,
                new ManualPreReserveDto { FolioCompra = query.FolioCompra, Numeros = ["60002"] }, token);
            True(!missing.Ok, "No debe reservarse un boleto inexistente.");
            var missingPurchase = await service.ConfirmPurchaseAsync(seeded.UserId,
                new ConfirmTicketPurchaseDto
                {
                    SorteoId = lottery.Id, FolioCompra = query.FolioCompra, Numeros = ["60002"]
                }, token);
            Equal("tickets_unavailable", missingPurchase.Codigo);

            foreach (var invalid in new[] { "-1", "abc", "60001-60000", "0-2147483647" })
                await ThrowsAsync<InvalidOperationException>(() => service.QueryManualAsync(
                    seeded.UserId, new ManualTicketQueryDto { Numeros = invalid }, token));
            var upperRange = await service.QueryManualAsync(seeded.UserId,
                new ManualTicketQueryDto { Numeros = "2147483646-2147483647" }, token);
            Equal(2, upperRange.Boletos.Count);
            True(upperRange.Boletos.All(x => !x.Disponible), "Los números no registrados siguen indisponibles.");

            query = await service.QueryManualAsync(seeded.UserId,
                new ManualTicketQueryDto { Numeros = "60000" }, token);
            True(query.Boletos.Single().Disponible, "60000 debe poder consultarse individualmente.");
            var reservation = random
                ? await service.PreReserveRandomAsync(seeded.UserId,
                    new RandomPreReserveDto { Cantidad = 1, Tipo = "inicio", Valor = "60000" }, token)
                : await service.PreReserveManualAsync(seeded.UserId,
                    new ManualPreReserveDto { FolioCompra = query.FolioCompra, Numeros = ["60000"] }, token);
            True(reservation.Ok, "60000 debe poder preapartarse por ambas pestañas.");
            Equal("60000", reservation.Numeros.Single());
            var purchase = await service.ConfirmPurchaseAsync(seeded.UserId,
                new ConfirmTicketPurchaseDto
                {
                    SorteoId = lottery.Id, FolioCompra = reservation.FolioCompra, Numeros = ["60000"]
                }, token);
            True(purchase.Ok, "60000 debe poder comprarse por ambas pestañas.");
            Equal(9m, purchase.Saldo);
            Equal(1, await context.BoletosConfirmados.CountAsync(x => x.SorteosId == lottery.Id && x.Numero == "60000"));
            var sold = await service.QueryManualAsync(seeded.UserId,
                new ManualTicketQueryDto { Numeros = "60000" }, token);
            True(!sold.Boletos.Single().Disponible, "El boleto vendido no debe volver a estar disponible.");
        }
        finally
        {
            await DeleteDatabaseAsync(options);
        }
    }
}

static async Task TestReferralEndToEndFlowAsync()
{
    var options = CreateOptions($"eLotto_ReferralEndToEnd_{Guid.NewGuid():N}");
    try
    {
        int referrerId;
        int referredId;
        int lotteryId;
        int depositTransactionId;
        string paymentIntentId;
        await using (var setup = new eLottoContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            setup.Rol.Add(new Rol { Name = "User" });
            var referrer = ExistingUser("flowowner", "5216624900000", "FLOWA001");
            setup.Users.Add(referrer);
            setup.ReferralProgramSettings.Add(new ReferralProgramSettings
            {
                Id = 1,
                IsActive = true,
                DepositRewardPercentage = 10m,
                MaxRewardedDeposits = 5,
                WinnerCashRewardAmount = 5000m,
                MinimumConfirmedTickets = 3,
                UpdatedAt = ApplicationClock.Now
            });
            await setup.SaveChangesAsync();
            referrerId = referrer.Id;

            var registration = await CreateAccountService(setup).CreateUserAsync(
                RegistrationRequest("flowreferred", "5216624900001", " flowa001 "));
            True(ResponseOk(registration), ResponseMessage(registration));
            var referred = await setup.Users.SingleAsync(user => user.User == "flowreferred");
            referredId = referred.Id;
            Equal(referrerId, referred.ReferredByUserId);
            True(
                ReferralCodeGenerator.IsValid(referred.ReferralCode) &&
                referred.ReferralCode != referrer.ReferralCode,
                "El referido debe recibir un código propio válido y diferente.");

            var referredWallet = new UserWallet
            {
                UserId = referred.Id,
                Balance = 0m,
                CreatedAt = ApplicationClock.Now,
                UpdatedAt = ApplicationClock.Now
            };
            var lottery = new Sorteos
            {
                Nombre = "Sorteo integral de referidos",
                Imagen1 = "referral-flow.webp",
                Fecha = ApplicationClock.Now.AddDays(1),
                PrecioBoleto = 1m,
                PrecioPorMil = 1m,
                CantidadBoletos = 100,
                PorcentajeMinimoVenta = 1,
                RascaditosHabilitados = false
            };
            setup.UserWallets.Add(referredWallet);
            setup.Sorteos.Add(lottery);
            await setup.SaveChangesAsync();
            lotteryId = lottery.Id;

            paymentIntentId = $"pi_{Guid.NewGuid():N}";
            var deposit = new WalletTransaction
            {
                UserId = referred.Id,
                WalletId = referredWallet.Id,
                SorteoId = lottery.Id,
                Type = WalletTransactionType.Deposit,
                Amount = 1000m,
                Status = WalletTransactionStatus.Pending,
                StripePaymentIntentId = paymentIntentId,
                Description = "Depósito integral referido",
                CreatedAt = ApplicationClock.Now
            };
            setup.WalletTransactions.Add(deposit);
            await setup.SaveChangesAsync();
            depositTransactionId = deposit.Id;
        }

        var succeededEvent = CreateSucceededEvent(
            $"evt_{Guid.NewGuid():N}",
            paymentIntentId,
            depositTransactionId,
            referredId,
            1000m);
        await ProcessEventAsync(options, succeededEvent);
        await ProcessEventAsync(options, succeededEvent);

        await using (var tickets = new eLottoContext(options))
        {
            var lottery = await tickets.Sorteos.SingleAsync(x => x.Id == lotteryId);
            lottery.Fecha = ApplicationClock.Now.AddHours(-1);
            var referrer = await tickets.Users.SingleAsync(x => x.Id == referrerId);
            var referred = await tickets.Users.SingleAsync(x => x.Id == referredId);
            AddReferrerTickets(tickets, lotteryId, referrer, 3, 10);
            tickets.BoletosConfirmados.Add(new BoletosConfirmados
            {
                SorteosId = lotteryId,
                Numero = "042",
                FolioCompra = "FLOWWIN1",
                UsuarioId = referred.Id,
                Fecha = ApplicationClock.Now.AddMinutes(-10),
                UsuarioIdConfirm = referred.Id,
                WhatsAppConfirm = referred.WhatsApp,
                CuentaAsignada = string.Empty
            });
            await tickets.SaveChangesAsync();
        }

        await FinalizeLotteryAsync(options, lotteryId);

        await using var verification = new eLottoContext(options);
        var referrerWallet = await verification.UserWallets.AsNoTracking()
            .SingleAsync(wallet => wallet.UserId == referrerId);
        Equal(100m, referrerWallet.Balance);
        Equal(1100m, await verification.UserWallets.AsNoTracking()
            .Where(wallet => wallet.UserId == referredId)
            .Select(wallet => wallet.Balance)
            .SingleAsync());
        Equal(1, await verification.WalletTransactions.CountAsync(transaction =>
            transaction.UserId == referrerId &&
            transaction.Type == WalletTransactionType.ReferralDepositReward));
        Equal(1, await verification.WalletTransactions.CountAsync(transaction =>
            transaction.UserId == referredId &&
            transaction.Type == WalletTransactionType.ReferralDepositReward &&
            transaction.Description == "Bono por depósito como referido"));
        Equal(1, await verification.ReferralDepositRewards.CountAsync(reward =>
            reward.ReferrerUserId == referrerId &&
            reward.ReferredUserId == referredId &&
            reward.RewardAmount == 100m));
        Equal(1, await verification.ReferralWinnerCashRewards.CountAsync(reward =>
            reward.ReferrerUserId == referrerId &&
            reward.Status == ReferralWinnerCashRewardStatus.Paid &&
            reward.PaidAt != null &&
            reward.RewardAmount == 5000m));
        Equal(
            1,
            await verification.WalletTransactions.CountAsync(transaction =>
                transaction.UserId == referrerId),
            "El premio en efectivo no debe crear una transacción Wallet adicional.");

        var view = await new MyReferralsService(verification, new SorteoTimeService())
            .GetAsync(referrerId, CancellationToken.None);
        Equal(1, view.DirectReferralsCount);
        Equal("Usuario flowreferred", view.Referrals.Single().Name);
        Equal(100m, view.DepositRewardsTotal);
        Equal(0m, view.PendingCashRewardsTotal);
        Equal(5000m, view.PaidCashRewardsTotal);
        Equal(1, view.DepositRewards.Count);
        Equal(10m, view.DepositRewards.Single().PercentageApplied);
        Equal(1, view.CashRewards.Count);
        Equal(3, view.CashRewards.Single().RequiredTickets);
        Equal(3, view.CashRewards.Single().ActualTickets);
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}

static async Task TestMyReferralsEmptyAndSecurityAsync()
{
    var options = CreateOptions($"eLotto_MyReferralsEmpty_{Guid.NewGuid():N}");
    try
    {
        int userId;
        await using (var setup = new eLottoContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            var user = CreateReferralViewUser("Usuario sin referidos", "EMPTY001", 1);
            setup.Users.Add(user);
            await setup.SaveChangesAsync();
            userId = user.Id;
        }

        await using var context = new eLottoContext(options);
        var service = new MyReferralsService(context, new SorteoTimeService());
        var response = await service.GetAsync(userId, CancellationToken.None);
        Equal(0, response.DirectReferralsCount);
        Equal(0m, response.DepositRewardsTotal);
        Equal(0m, response.PendingCashRewardsTotal);
        Equal(0m, response.PaidCashRewardsTotal);
        Equal(0, response.Referrals.Count);
        Equal(0, response.DepositRewards.Count);
        Equal(0, response.CashRewards.Count);

        var authorization = typeof(MyReferralsController)
            .GetCustomAttribute<AuthorizeAttribute>();
        True(authorization != null, "El endpoint de Mis referidos debe requerir autorización.");
        Equal("User", authorization!.Roles);

        var getMethod = typeof(MyReferralsController).GetMethod("Get")!;
        True(
            getMethod.GetParameters().All(parameter => parameter.ParameterType == typeof(CancellationToken)),
            "El endpoint no debe aceptar IDs de usuario o referidor.");

        var controller = new MyReferralsController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
                        "Test"))
                }
            }
        };
        var result = await controller.Get(CancellationToken.None) as OkObjectResult;
        True(result?.Value is MyReferralsResponse, "El controlador debe resolver la consulta desde el JWT.");
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}

static async Task TestMyReferralsHistoryAsync()
{
    var options = CreateOptions($"eLotto_MyReferralsHistory_{Guid.NewGuid():N}");
    try
    {
        int referrerId;
        int directReferralId;
        int childReferralId;
        await using (var setup = new eLottoContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            var referrer = CreateReferralViewUser("Alfredo", "VIEWA001", 10);
            var directReferral = CreateReferralViewUser("Carlos", "VIEWB001", 11);
            var childReferral = CreateReferralViewUser("Pedro", "VIEWC001", 12);
            directReferral.IsActive = false;
            directReferral.Referrer = referrer;
            childReferral.Referrer = directReferral;
            setup.Users.AddRange(referrer, directReferral, childReferral);
            await setup.SaveChangesAsync();
            referrerId = referrer.Id;
            directReferralId = directReferral.Id;
            childReferralId = childReferral.Id;

            var referrerWallet = new UserWallet
            {
                UserId = referrer.Id,
                Balance = 250m,
                CreatedAt = ApplicationClock.Now,
                UpdatedAt = ApplicationClock.Now
            };
            var referralWallet = new UserWallet
            {
                UserId = directReferral.Id,
                Balance = 0m,
                CreatedAt = ApplicationClock.Now,
                UpdatedAt = ApplicationClock.Now
            };
            setup.UserWallets.AddRange(referrerWallet, referralWallet);
            await setup.SaveChangesAsync();

            var firstLottery = new Sorteos
            {
                Nombre = "Sorteo pendiente", Imagen1 = "pending.webp",
                Fecha = ApplicationClock.Now.AddDays(-2),
                PrecioBoleto = 1m, PrecioPorMil = 1m, CantidadBoletos = 100
            };
            var secondLottery = new Sorteos
            {
                Nombre = "Sorteo pagado", Imagen1 = "paid.webp",
                Fecha = ApplicationClock.Now.AddDays(-1),
                PrecioBoleto = 1m, PrecioPorMil = 1m, CantidadBoletos = 100
            };
            setup.Sorteos.AddRange(firstLottery, secondLottery);
            await setup.SaveChangesAsync();

            var firstDeposit = CreateReferralViewTransaction(
                directReferral.Id, referralWallet.Id, WalletTransactionType.Deposit, 1000m, "Depósito 1");
            var firstReward = CreateReferralViewTransaction(
                referrer.Id, referrerWallet.Id, WalletTransactionType.ReferralDepositReward, 100m, "Bono 1");
            var secondDeposit = CreateReferralViewTransaction(
                directReferral.Id, referralWallet.Id, WalletTransactionType.Deposit, 3000m, "Depósito 2");
            var secondReward = CreateReferralViewTransaction(
                referrer.Id, referrerWallet.Id, WalletTransactionType.ReferralDepositReward, 150m, "Bono 2");
            firstDeposit.SorteoId = firstReward.SorteoId = firstLottery.Id;
            secondDeposit.SorteoId = secondReward.SorteoId = secondLottery.Id;
            setup.WalletTransactions.AddRange(firstDeposit, firstReward, secondDeposit, secondReward);
            await setup.SaveChangesAsync();

            setup.ReferralDepositRewards.AddRange(
                new ReferralDepositReward
                {
                    ReferredUserId = directReferral.Id,
                    ReferrerUserId = referrer.Id,
                    SourceDepositTransactionId = firstDeposit.Id,
                    RewardWalletTransactionId = firstReward.Id,
                    DepositAmount = 1000m,
                    PercentageApplied = 10m,
                    RewardAmount = 100m,
                    CreatedAt = ApplicationClock.Now.AddDays(-2)
                },
                new ReferralDepositReward
                {
                    ReferredUserId = directReferral.Id,
                    ReferrerUserId = referrer.Id,
                    SourceDepositTransactionId = secondDeposit.Id,
                    RewardWalletTransactionId = secondReward.Id,
                    DepositAmount = 3000m,
                    PercentageApplied = 5m,
                    RewardAmount = 150m,
                    CreatedAt = ApplicationClock.Now.AddDays(-1)
                });

            setup.GanadoresSorteos.AddRange(
                CreateReferralViewWinner(
                    directReferral, referrer.Id, firstLottery.Id, "Sorteo pendiente", 100, 125, 5000m,
                    ReferralWinnerCashRewardStatus.Pending, null),
                CreateReferralViewWinner(
                    directReferral, referrer.Id, secondLottery.Id, "Sorteo pagado", 50, 70, 3000m,
                    ReferralWinnerCashRewardStatus.Paid, ApplicationClock.Now));
            setup.ReferralProgramSettings.Add(new ReferralProgramSettings
            {
                Id = 1,
                IsActive = false,
                DepositRewardPercentage = 99m,
                MaxRewardedDeposits = 0,
                WinnerCashRewardAmount = 1m,
                MinimumConfirmedTickets = 999,
                UpdatedAt = ApplicationClock.Now
            });
            await setup.SaveChangesAsync();
        }

        await using var context = new eLottoContext(options);
        var service = new MyReferralsService(context, new SorteoTimeService());
        var response = await service.GetAsync(referrerId, CancellationToken.None);
        Equal(1, response.DirectReferralsCount);
        Equal("Carlos", response.Referrals.Single().Name);
        Equal(2, response.Referrals.Single().DepositRewardsCount);
        Equal(250m, response.Referrals.Single().DepositRewardsTotal);
        Equal(2, response.Referrals.Single().WinnerCashRewardsCount);
        True(
            response.Referrals.All(referral => referral.Name != "Pedro"),
            "Un referidor no debe ver usuarios de segundo nivel.");
        Equal(250m, response.DepositRewardsTotal);
        Equal(5m, response.DepositRewards.First().PercentageApplied);
        Equal(10m, response.DepositRewards.Last().PercentageApplied);
        Equal(5000m, response.PendingCashRewardsTotal);
        Equal(3000m, response.PaidCashRewardsTotal);
        var pendingReward = response.CashRewards.Single(
            reward => reward.Status == ReferralWinnerCashRewardStatus.Pending);
        var paidReward = response.CashRewards.Single(
            reward => reward.Status == ReferralWinnerCashRewardStatus.Paid);
        True(paidReward.PaidAt.HasValue, "El premio pagado debe exponer PaidAt.");
        Equal("Carlos", pendingReward.WinnerUserName);

        var childView = await service.GetAsync(directReferralId, CancellationToken.None);
        Equal(1, childView.DirectReferralsCount);
        Equal("Pedro", childView.Referrals.Single().Name);
        Equal(0, childView.Referrals.Single().DepositRewardsCount);
        Equal(0m, childView.Referrals.Single().DepositRewardsTotal);
        Equal(0, childView.Referrals.Single().WinnerCashRewardsCount);

        var grandchildView = await service.GetAsync(childReferralId, CancellationToken.None);
        Equal(0, grandchildView.DirectReferralsCount);
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}

static Users CreateReferralViewUser(string name, string referralCode, int suffix) =>
    new()
    {
        Name = name,
        Email = $"referral-view-{suffix}@example.test",
        WhatsApp = $"521662400{suffix:D4}",
        User = $"refview{suffix}",
        Password = "not-used",
        IsActive = true,
        Date = ApplicationClock.Now.AddDays(-suffix),
        Languaje = "es",
        ReferralCode = referralCode
    };

static WalletTransaction CreateReferralViewTransaction(
    int userId,
    int walletId,
    WalletTransactionType type,
    decimal amount,
    string description) =>
    new()
    {
        UserId = userId,
        WalletId = walletId,
        Type = type,
        Amount = amount,
        Status = WalletTransactionStatus.Completed,
        Description = description,
        CreatedAt = ApplicationClock.Now,
        CompletedAt = ApplicationClock.Now
    };

static GanadoresSorteos CreateReferralViewWinner(
    Users winner,
    int referrerUserId,
    int sorteoId,
    string lotteryName,
    int requiredTickets,
    int actualTickets,
    decimal amount,
    ReferralWinnerCashRewardStatus status,
    DateTime? paidAt) =>
    new()
    {
        SorteoId = sorteoId,
        NumeroGanador = "042",
        FolioSorteo = Guid.NewGuid().ToString("N"),
        Nombre = lotteryName,
        FechaFin = ApplicationClock.Now,
        WhatsAppGanador = winner.WhatsApp,
        UsuarioIdGanador = winner.Id,
        NombreGanador = winner.User,
        ReferralWinnerCashReward = new ReferralWinnerCashReward
        {
            ReferrerUserId = referrerUserId,
            RequiredTickets = requiredTickets,
            ActualTickets = actualTickets,
            RewardAmount = amount,
            Status = status,
            CreatedAt = ApplicationClock.Now,
            PaidAt = paidAt
        }
    };

static Task TestReferralProgramSettingsValidationAsync()
{
    var valid = new UpdateReferralProgramSettingsRequest
    {
        IsActive = true,
        DepositRewardPercentage = 0m,
        MaxRewardedDeposits = 0,
        WinnerCashRewardAmount = 0m,
        MinimumConfirmedTickets = 0,
        RowVersion = new byte[8]
    };
    True(Validate(valid).Count == 0, "Los valores cero acordados deben ser válidos.");

    var invalid = new UpdateReferralProgramSettingsRequest
    {
        IsActive = true,
        DepositRewardPercentage = 100.001m,
        MaxRewardedDeposits = -1,
        WinnerCashRewardAmount = -0.01m,
        MinimumConfirmedTickets = -1,
        RowVersion = new byte[7]
    };
    var errors = Validate(invalid);
    True(errors.Count >= 5, "Deben rechazarse rangos, precisión y RowVersion inválidos.");

    return Task.CompletedTask;
}

static async Task TestReferralProgramSettingsConcurrencyAsync()
{
    var options = CreateOptions($"eLotto_ReferralSettingsConcurrency_{Guid.NewGuid():N}");
    try
    {
        await using (var setup = new eLottoContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            setup.ReferralProgramSettings.Add(new ReferralProgramSettings
            {
                Id = 1,
                IsActive = true,
                DepositRewardPercentage = 10m,
                MaxRewardedDeposits = 5,
                WinnerCashRewardAmount = 5000m,
                MinimumConfirmedTickets = 100,
                UpdatedAt = ApplicationClock.Now
            });
            await setup.SaveChangesAsync();
        }

        await using var firstContext = new eLottoContext(options);
        await using var secondContext = new eLottoContext(options);
        var firstRepository = new ReferralProgramSettingsRepository(firstContext);
        var secondRepository = new ReferralProgramSettingsRepository(secondContext);
        var firstSnapshot = await firstRepository.GetAsync();
        var secondSnapshot = await secondRepository.GetAsync();

        var firstResult = await firstRepository.UpdateAsync(
            SettingsValues(false, 8m, 4, 4000m, 80),
            firstSnapshot.RowVersion);
        Equal(ReferralProgramSettingsUpdateStatus.Updated, firstResult.Status);
        True(!firstResult.Settings.RowVersion.SequenceEqual(firstSnapshot.RowVersion), "El RowVersion debe cambiar al actualizar.");

        var staleResult = await secondRepository.UpdateAsync(
            SettingsValues(true, 12m, 6, 6000m, 120),
            secondSnapshot.RowVersion);
        Equal(ReferralProgramSettingsUpdateStatus.ConcurrencyConflict, staleResult.Status);

        await using var verification = new eLottoContext(options);
        var persisted = await verification.ReferralProgramSettings.AsNoTracking().SingleAsync();
        True(!persisted.IsActive, "La actualización concurrente obsoleta no debe sobrescribir la primera.");
        Equal(8m, persisted.DepositRewardPercentage);
        Equal(4, persisted.MaxRewardedDeposits);
        Equal(4000m, persisted.WinnerCashRewardAmount);
        Equal(80, persisted.MinimumConfirmedTickets);
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}

static Task TestReferralProgramSettingsAuthorizationAsync()
{
    var authorization = typeof(ReferralProgramSettingsController)
        .GetCustomAttribute<AuthorizeAttribute>();
    True(authorization != null, "El controlador debe requerir autorización.");
    Equal("Admin", authorization!.Roles);

    var publicMethods = typeof(ReferralProgramSettingsController)
        .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
        .Select(method => method.Name)
        .OrderBy(name => name)
        .ToArray();
    True(publicMethods.SequenceEqual(new[] { "Get", "Update" }), "La API singleton solo debe exponer GET y PUT.");
    return Task.CompletedTask;
}

static async Task TestReferralInvitationAsync()
{
    var authorization = typeof(ReferralInvitationController)
        .GetCustomAttribute<AuthorizeAttribute>();
    True(authorization != null, "El controlador de invitación debe requerir autorización.");
    Equal("User", authorization!.Roles);

    var publicMethods = typeof(ReferralInvitationController)
        .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
        .Select(method => method.Name)
        .ToArray();
    True(publicMethods.SequenceEqual(new[] { "Get" }), "La API de invitación solo debe exponer GET.");

    var options = CreateOptions($"eLotto_ReferralInvitation_{Guid.NewGuid():N}");
    try
    {
        await using var context = new eLottoContext(options);
        await context.Database.EnsureCreatedAsync();
        var owner = new Users
        {
            Name = "Usuario invitador",
            Email = "inviter@example.test",
            WhatsApp = "5216621000100",
            User = "inviter",
            Password = "not-used",
            IsActive = true,
            Date = ApplicationClock.Now,
            Languaje = "es",
            ReferralCode = "ABC12345"
        };
        var other = new Users
        {
            Name = "Otro usuario",
            Email = "other-inviter@example.test",
            WhatsApp = "5216621000101",
            User = "otherinviter",
            Password = "not-used",
            IsActive = true,
            Date = ApplicationClock.Now,
            Languaje = "es",
            ReferralCode = "ZZZZZZZZ"
        };
        context.Users.AddRange(owner, other);
        await context.SaveChangesAsync();

        var repository = new UserRepository(context);
        Equal("ABC12345", await repository.GetReferralCodeAsync(owner.Id));

        var controller = new ReferralInvitationController(repository)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, owner.Id.ToString()) },
                        "Test"))
                }
            }
        };

        var result = await controller.Get();
        var ok = result as OkObjectResult;
        True(ok != null, "La invitación del usuario autenticado debe responder 200.");
        var response = ok!.Value as ReferralInvitationResponse;
        True(response != null, "La respuesta debe usar el contrato de invitación.");
        Equal("ABC12345", response!.ReferralCode);
        True(response.ReferralCode != other.ReferralCode, "No debe devolverse el código de otro usuario.");
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}

static async Task TestDepositWithoutReferrerRewardAsync()
{
    var options = CreateOptions($"eLotto_NoReferralReward_{Guid.NewGuid():N}");
    try
    {
        var seeded = await RecreateAndSeedAsync(
            options,
            prizeQuantity: 1,
            depositAmount: 2000m);
        await using (var configuration = new eLottoContext(options))
        {
            var lottery = await configuration.Sorteos.SingleAsync(x => x.Id == seeded.SorteoId);
            lottery.RascaditosHabilitados = false;
            lottery.GanadoresPorGrupo = 0;
            lottery.RascaditosPorGrupo = 0;
            lottery.ImporteDepositoStripePorRascadito = 0m;
            await configuration.SaveChangesAsync();
        }
        await ProcessEventAsync(
            options,
            CreateSucceededEvent(
                "evt_no_referrer",
                seeded.PaymentIntentId,
                seeded.TransactionId,
                seeded.UserId,
                2000m));

        await using var verification = new eLottoContext(options);
        Equal(2000m, await verification.UserWallets
            .Where(x => x.Id == seeded.WalletId)
            .Select(x => x.Balance)
            .SingleAsync());
        Equal(0, await verification.ReferralDepositRewards.CountAsync());
        Equal(0, await verification.WalletTransactions.CountAsync(
            x => x.Type == WalletTransactionType.ReferralDepositReward));
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}

static async Task TestReferralDepositRewardAsync()
{
    var options = CreateOptions($"eLotto_ReferralReward_{Guid.NewGuid():N}");
    try
    {
        var scenario = await CreateReferralDepositScenarioAsync(
            options,
            depositAmount: 2000m,
            percentage: 10m,
            maximumRewards: 5,
            createReferrerWallet: false,
            referrerIsActive: false);

        ISorteoTimeService timeService = new SorteoTimeService();
        await using (var setup = new eLottoContext(options))
        {
            var sorteo = await setup.Sorteos.SingleAsync(x => x.Id == scenario.Deposit.SorteoId);
            sorteo.ZonaHoraria = "Cancun";
            await setup.SaveChangesAsync();
        }
        var serverBefore = ApplicationClock.NowOffset;
        await ProcessEventAsync(
            options,
            CreateSucceededEvent(
                "evt_referral_reward",
                scenario.Deposit.PaymentIntentId,
                scenario.Deposit.TransactionId,
                scenario.Deposit.UserId,
                2000m));
        var serverAfter = ApplicationClock.NowOffset;

        await using var verification = new eLottoContext(options);
        var sorteoActual = await verification.Sorteos.AsNoTracking()
            .SingleAsync(x => x.Id == scenario.Deposit.SorteoId);
        var zoneBefore = timeService.ConvertToSorteoTime(serverBefore, sorteoActual).DateTime;
        var zoneAfter = timeService.ConvertToSorteoTime(serverAfter, sorteoActual).DateTime;
        Equal(2200m, await verification.UserWallets
            .Where(x => x.Id == scenario.Deposit.WalletId)
            .Select(x => x.Balance)
            .SingleAsync());
        var referrerWallet = await verification.UserWallets
            .AsNoTracking()
            .SingleAsync(x => x.UserId == scenario.ReferrerUserId);
        Equal(200m, referrerWallet.Balance);
        True(referrerWallet.CreatedAt >= serverBefore.DateTime &&
            referrerWallet.CreatedAt <= serverAfter.DateTime,
            "La cartera compartida del referidor debe crearse con la hora del servidor.");

        var rewardTransactions = await verification.WalletTransactions
            .AsNoTracking()
            .Where(x => x.Type == WalletTransactionType.ReferralDepositReward)
            .ToListAsync();
        Equal(2, rewardTransactions.Count);
        True(rewardTransactions.All(x =>
            x.SorteoId == scenario.Deposit.SorteoId &&
            x.CreatedAt >= zoneBefore && x.CreatedAt <= zoneAfter &&
            x.CompletedAt >= zoneBefore && x.CompletedAt <= zoneAfter),
            "Ambos movimientos de bono deben conservar la hora y el sorteo del depósito.");
        var rewardTransaction = rewardTransactions.Single(x => x.UserId == scenario.ReferrerUserId);
        Equal(scenario.ReferrerUserId, rewardTransaction.UserId);
        Equal(referrerWallet.Id, rewardTransaction.WalletId);
        Equal(200m, rewardTransaction.Amount);
        Equal(WalletTransactionStatus.Completed, rewardTransaction.Status);
        Equal("Bono por depósito referido", rewardTransaction.Description);
        var referredRewardTransaction = rewardTransactions.Single(x => x.UserId == scenario.Deposit.UserId);
        Equal(scenario.Deposit.WalletId, referredRewardTransaction.WalletId);
        Equal(rewardTransaction.Amount, referredRewardTransaction.Amount);
        Equal(WalletTransactionStatus.Completed, referredRewardTransaction.Status);
        Equal("Bono por depósito como referido", referredRewardTransaction.Description);

        var reward = await verification.ReferralDepositRewards
            .AsNoTracking()
            .SingleAsync();
        Equal(scenario.Deposit.UserId, reward.ReferredUserId);
        Equal(scenario.ReferrerUserId, reward.ReferrerUserId);
        Equal(scenario.Deposit.TransactionId, reward.SourceDepositTransactionId);
        Equal(rewardTransaction.Id, reward.RewardWalletTransactionId);
        Equal(2000m, reward.DepositAmount);
        Equal(10m, reward.PercentageApplied);
        Equal(200m, reward.RewardAmount);
        True(reward.CreatedAt >= zoneBefore && reward.CreatedAt <= zoneAfter,
            "El registro del bono debe usar la hora del sorteo.");

        var referredView = await new MyReferralsService(verification, new SorteoTimeService())
            .GetAsync(scenario.Deposit.UserId, CancellationToken.None);
        Equal(0m, referredView.DepositRewardsTotal,
            "El bono personal no debe aparecer como generado por referidos propios.");

        var roundedDeposit = await AddPendingDepositAsync(
            options,
            scenario.Deposit.UserId,
            scenario.Deposit.WalletId,
            scenario.Deposit.SorteoId,
            10.05m);
        await ProcessEventAsync(
            options,
            CreateSucceededEvent(
                "evt_referral_rounding",
                roundedDeposit.PaymentIntentId,
                roundedDeposit.TransactionId,
                scenario.Deposit.UserId,
                10.05m));
        Equal(
            1.01m,
            await verification.ReferralDepositRewards
                .Where(x => x.SourceDepositTransactionId == roundedDeposit.TransactionId)
                .Select(x => x.RewardAmount)
                .SingleAsync());
        Equal(2, await verification.WalletTransactions.CountAsync(x =>
            x.Type == WalletTransactionType.ReferralDepositReward &&
            x.Amount == 1.01m));
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}

static async Task TestDisabledReferralDepositRewardsAsync()
{
    var options = CreateOptions($"eLotto_DisabledReferralRewards_{Guid.NewGuid():N}");
    try
    {
        var scenario = await CreateReferralDepositScenarioAsync(
            options,
            depositAmount: 100m,
            percentage: 10m,
            maximumRewards: 5,
            isActive: false);

        await ProcessEventAsync(options, CreateSucceededEvent(
            "evt_referral_inactive",
            scenario.Deposit.PaymentIntentId,
            scenario.Deposit.TransactionId,
            scenario.Deposit.UserId,
            100m));

        await UpdateReferralSettingsAsync(options, true, 0m, 5);
        var percentageZero = await AddPendingDepositAsync(
            options, scenario.Deposit.UserId, scenario.Deposit.WalletId, scenario.Deposit.SorteoId, 100m);
        await ProcessEventAsync(options, CreateSucceededEvent(
            "evt_referral_percentage_zero",
            percentageZero.PaymentIntentId,
            percentageZero.TransactionId,
            scenario.Deposit.UserId,
            100m));

        await UpdateReferralSettingsAsync(options, true, 10m, 0);
        var maximumZero = await AddPendingDepositAsync(
            options, scenario.Deposit.UserId, scenario.Deposit.WalletId, scenario.Deposit.SorteoId, 100m);
        await ProcessEventAsync(options, CreateSucceededEvent(
            "evt_referral_maximum_zero",
            maximumZero.PaymentIntentId,
            maximumZero.TransactionId,
            scenario.Deposit.UserId,
            100m));

        await UpdateReferralSettingsAsync(options, true, 1m, 5);
        var roundedZero = await AddPendingDepositAsync(
            options, scenario.Deposit.UserId, scenario.Deposit.WalletId, scenario.Deposit.SorteoId, 0.01m);
        await ProcessEventAsync(options, CreateSucceededEvent(
            "evt_referral_rounds_zero",
            roundedZero.PaymentIntentId,
            roundedZero.TransactionId,
            scenario.Deposit.UserId,
            0.01m));

        await using var verification = new eLottoContext(options);
        Equal(0, await verification.ReferralDepositRewards.CountAsync());
        Equal(0, await verification.WalletTransactions.CountAsync(
            x => x.Type == WalletTransactionType.ReferralDepositReward));
        Equal(300.01m, await verification.UserWallets
            .Where(x => x.Id == scenario.Deposit.WalletId)
            .Select(x => x.Balance)
            .SingleAsync());
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}

static async Task TestReferralDepositRewardLimitChangesAsync()
{
    var options = CreateOptions($"eLotto_ReferralLimitChanges_{Guid.NewGuid():N}");
    try
    {
        var scenario = await CreateReferralDepositScenarioAsync(
            options,
            depositAmount: 100m,
            percentage: 10m,
            maximumRewards: 5,
            createReferrerWallet: true);
        await ProcessEventAsync(options, CreateSucceededEvent(
            "evt_limit_1",
            scenario.Deposit.PaymentIntentId,
            scenario.Deposit.TransactionId,
            scenario.Deposit.UserId,
            100m));

        for (var number = 2; number <= 7; number++)
        {
            var deposit = await AddPendingDepositAsync(
                options, scenario.Deposit.UserId, scenario.Deposit.WalletId, scenario.Deposit.SorteoId, 100m);
            await ProcessEventAsync(options, CreateSucceededEvent(
                $"evt_limit_{number}",
                deposit.PaymentIntentId,
                deposit.TransactionId,
                scenario.Deposit.UserId,
                100m));
        }
        await AssertReferralRewardCountAsync(options, scenario.Deposit.UserId, 5);

        await UpdateReferralSettingsAsync(options, true, 10m, 10);
        var sixth = await AddPendingDepositAsync(
            options, scenario.Deposit.UserId, scenario.Deposit.WalletId, scenario.Deposit.SorteoId, 100m);
        await ProcessEventAsync(options, CreateSucceededEvent(
            "evt_limit_8_new_sixth",
            sixth.PaymentIntentId,
            sixth.TransactionId,
            scenario.Deposit.UserId,
            100m));
        await AssertReferralRewardCountAsync(options, scenario.Deposit.UserId, 6);

        await UpdateReferralSettingsAsync(options, true, 10m, 4);
        var reduced = await AddPendingDepositAsync(
            options, scenario.Deposit.UserId, scenario.Deposit.WalletId, scenario.Deposit.SorteoId, 100m);
        await ProcessEventAsync(options, CreateSucceededEvent(
            "evt_limit_reduced",
            reduced.PaymentIntentId,
            reduced.TransactionId,
            scenario.Deposit.UserId,
            100m));
        await AssertReferralRewardCountAsync(options, scenario.Deposit.UserId, 6);

        await UpdateReferralSettingsAsync(options, true, 10m, 8);
        for (var number = 7; number <= 8; number++)
        {
            var deposit = await AddPendingDepositAsync(
                options, scenario.Deposit.UserId, scenario.Deposit.WalletId, scenario.Deposit.SorteoId, 100m);
            await ProcessEventAsync(options, CreateSucceededEvent(
                $"evt_limit_increased_{number}",
                deposit.PaymentIntentId,
                deposit.TransactionId,
                scenario.Deposit.UserId,
                100m));
        }
        await AssertReferralRewardCountAsync(options, scenario.Deposit.UserId, 8);
        await using var verification = new eLottoContext(options);
        Equal(8, await verification.WalletTransactions.CountAsync(x =>
            x.UserId == scenario.Deposit.UserId &&
            x.Type == WalletTransactionType.ReferralDepositReward));
        Equal(8, await verification.WalletTransactions.CountAsync(x =>
            x.UserId == scenario.ReferrerUserId &&
            x.Type == WalletTransactionType.ReferralDepositReward));
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}

static async Task TestIndependentReferralDepositLimitsAsync()
{
    var options = CreateOptions($"eLotto_IndependentReferralLimits_{Guid.NewGuid():N}");
    try
    {
        var scenario = await CreateReferralDepositScenarioAsync(
            options,
            depositAmount: 100m,
            percentage: 10m,
            maximumRewards: 1,
            createReferrerWallet: false);
        var second = await AddReferredUserDepositAsync(
            options,
            scenario.ReferrerUserId,
            scenario.Deposit.SorteoId,
            100m,
            "SECOND01");

        await Task.WhenAll(
            ProcessEventAsync(options, CreateSucceededEvent(
                "evt_independent_first",
                scenario.Deposit.PaymentIntentId,
                scenario.Deposit.TransactionId,
                scenario.Deposit.UserId,
                100m)),
            ProcessEventAsync(options, CreateSucceededEvent(
                "evt_independent_second",
                second.PaymentIntentId,
                second.TransactionId,
                second.UserId,
                100m)));

        await using var verification = new eLottoContext(options);
        Equal(1, await verification.ReferralDepositRewards.CountAsync(
            x => x.ReferredUserId == scenario.Deposit.UserId));
        Equal(1, await verification.ReferralDepositRewards.CountAsync(
            x => x.ReferredUserId == second.UserId));
        Equal(4, await verification.WalletTransactions.CountAsync(
            x => x.Type == WalletTransactionType.ReferralDepositReward));
        Equal(20m, await verification.UserWallets
            .Where(x => x.UserId == scenario.ReferrerUserId)
            .Select(x => x.Balance)
            .SingleAsync());
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}

static async Task TestConcurrentReferralDepositLimitAsync()
{
    var options = CreateOptions($"eLotto_ConcurrentReferralLimit_{Guid.NewGuid():N}");
    try
    {
        var scenario = await CreateReferralDepositScenarioAsync(
            options,
            depositAmount: 100m,
            percentage: 10m,
            maximumRewards: 2,
            createReferrerWallet: true);
        var initialEvent = CreateSucceededEvent(
            "evt_concurrent_referral_initial",
            scenario.Deposit.PaymentIntentId,
            scenario.Deposit.TransactionId,
            scenario.Deposit.UserId,
            100m);
        await ProcessEventAsync(options, initialEvent);
        await ProcessEventAsync(options, initialEvent);
        await ProcessEventAsync(options, CreateSucceededEvent(
            "evt_concurrent_referral_initial_retry",
            scenario.Deposit.PaymentIntentId,
            scenario.Deposit.TransactionId,
            scenario.Deposit.UserId,
            100m));

        var left = await AddPendingDepositAsync(
            options, scenario.Deposit.UserId, scenario.Deposit.WalletId, scenario.Deposit.SorteoId, 100m);
        var right = await AddPendingDepositAsync(
            options, scenario.Deposit.UserId, scenario.Deposit.WalletId, scenario.Deposit.SorteoId, 100m);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task ProcessAsync(string eventId, PendingDeposit deposit)
        {
            await start.Task;
            await ProcessEventAsync(options, CreateSucceededEvent(
                eventId,
                deposit.PaymentIntentId,
                deposit.TransactionId,
                scenario.Deposit.UserId,
                100m));
        }

        var leftTask = Task.Run(() => ProcessAsync("evt_concurrent_referral_left", left));
        var rightTask = Task.Run(() => ProcessAsync("evt_concurrent_referral_right", right));
        start.SetResult();
        await Task.WhenAll(leftTask, rightTask);

        await using var verification = new eLottoContext(options);
        Equal(2, await verification.ReferralDepositRewards.CountAsync());
        Equal(4, await verification.WalletTransactions.CountAsync(
            x => x.Type == WalletTransactionType.ReferralDepositReward));
        Equal(2, await verification.WalletTransactions.CountAsync(x =>
            x.UserId == scenario.Deposit.UserId &&
            x.Type == WalletTransactionType.ReferralDepositReward));
        Equal(20m, await verification.UserWallets
            .Where(x => x.UserId == scenario.ReferrerUserId)
            .Select(x => x.Balance)
            .SingleAsync());
        Equal(3, await verification.WalletTransactions.CountAsync(
            x => x.UserId == scenario.Deposit.UserId &&
                 x.Type == WalletTransactionType.Deposit &&
                 x.Status == WalletTransactionStatus.Completed));
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}

static async Task TestReferralDepositFailureBehaviorAsync()
{
    var missingOptions = CreateOptions($"eLotto_MissingReferralSettings_{Guid.NewGuid():N}");
    try
    {
        var scenario = await CreateReferralDepositScenarioAsync(
            missingOptions,
            depositAmount: 100m,
            percentage: 10m,
            maximumRewards: 5,
            includeSettings: false);
        var logger = new CollectingWalletLogger();
        await ProcessEventAsync(missingOptions, CreateSucceededEvent(
            "evt_missing_referral_settings",
            scenario.Deposit.PaymentIntentId,
            scenario.Deposit.TransactionId,
            scenario.Deposit.UserId,
            100m), logger);

        await using var verification = new eLottoContext(missingOptions);
        Equal(100m, await verification.UserWallets
            .Where(x => x.Id == scenario.Deposit.WalletId)
            .Select(x => x.Balance)
            .SingleAsync());
        Equal(0, await verification.ReferralDepositRewards.CountAsync());
        True(logger.Levels.Contains(LogLevel.Critical), "La ausencia del singleton debe registrarse como error crítico.");
    }
    finally
    {
        await DeleteDatabaseAsync(missingOptions);
    }

    var failureOptions = CreateOptions($"eLotto_ReferralRewardRollback_{Guid.NewGuid():N}");
    try
    {
        var scenario = await CreateReferralDepositScenarioAsync(
            failureOptions,
            depositAmount: 10m,
            percentage: 10m,
            maximumRewards: 5,
            createReferrerWallet: true,
            referrerBalance: 9999999999999999.99m);
        var failed = false;
        try
        {
            await ProcessEventAsync(failureOptions, CreateSucceededEvent(
                "evt_referral_reward_failure",
                scenario.Deposit.PaymentIntentId,
                scenario.Deposit.TransactionId,
                scenario.Deposit.UserId,
                10m));
        }
        catch
        {
            failed = true;
        }
        True(failed, "El desbordamiento controlado del wallet del referidor debe hacer fallar el evento.");

        await using var verification = new eLottoContext(failureOptions);
        Equal(WalletTransactionStatus.Pending, await verification.WalletTransactions
            .Where(x => x.Id == scenario.Deposit.TransactionId)
            .Select(x => x.Status)
            .SingleAsync());
        Equal(0m, await verification.UserWallets
            .Where(x => x.Id == scenario.Deposit.WalletId)
            .Select(x => x.Balance)
            .SingleAsync());
        Equal(9999999999999999.99m, await verification.UserWallets
            .Where(x => x.UserId == scenario.ReferrerUserId)
            .Select(x => x.Balance)
            .SingleAsync());
        Equal(0, await verification.ReferralDepositRewards.CountAsync());
        Equal(0, await verification.WalletTransactions.CountAsync(
            x => x.Type == WalletTransactionType.ReferralDepositReward));
    }
    finally
    {
        await DeleteDatabaseAsync(failureOptions);
    }

    var referredFailureOptions = CreateOptions($"eLotto_ReferredRewardRollback_{Guid.NewGuid():N}");
    try
    {
        var scenario = await CreateReferralDepositScenarioAsync(
            referredFailureOptions,
            depositAmount: 10m,
            percentage: 10m,
            maximumRewards: 5,
            createReferrerWallet: true,
            referredBalance: 9999999999999999.99m);

        await ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => ProcessEventAsync(
            referredFailureOptions,
            CreateSucceededEvent(
                "evt_referred_reward_failure",
                scenario.Deposit.PaymentIntentId,
                scenario.Deposit.TransactionId,
                scenario.Deposit.UserId,
                10m)));

        await using var verification = new eLottoContext(referredFailureOptions);
        Equal(WalletTransactionStatus.Pending, await verification.WalletTransactions
            .Where(x => x.Id == scenario.Deposit.TransactionId)
            .Select(x => x.Status)
            .SingleAsync());
        Equal(9999999999999999.99m, await verification.UserWallets
            .Where(x => x.Id == scenario.Deposit.WalletId)
            .Select(x => x.Balance)
            .SingleAsync());
        Equal(0m, await verification.UserWallets
            .Where(x => x.UserId == scenario.ReferrerUserId)
            .Select(x => x.Balance)
            .SingleAsync());
        Equal(0, await verification.ReferralDepositRewards.CountAsync());
        Equal(0, await verification.WalletTransactions.CountAsync(
            x => x.Type == WalletTransactionType.ReferralDepositReward));
    }
    finally
    {
        await DeleteDatabaseAsync(referredFailureOptions);
    }
}

static async Task TestReferralWinnerCashRewardAsync()
{
    var options = CreateOptions($"eLotto_ReferralWinnerReward_{Guid.NewGuid():N}");
    try
    {
        var scenario = await CreateReferralWinnerScenarioAsync(
            options,
            minimumTickets: 4,
            actualTickets: 4,
            rewardAmount: 5000m,
            referrerIsActive: false,
            zonaHoraria: "Cancun");
        ISorteoTimeService timeService = new SorteoTimeService();

        await using (var directRelationship = new eLottoContext(options))
        {
            var indirectReferrer = new Users
            {
                Name = "Referidor indirecto",
                Email = "indirect-referrer@example.test",
                WhatsApp = "5216623000099",
                User = "indirectreferrer",
                Password = "not-used",
                IsActive = true,
                Date = ApplicationClock.Now,
                Languaje = "es",
                ReferralCode = "WINREF02"
            };
            directRelationship.Users.Add(indirectReferrer);
            var directReferrer = await directRelationship.Users
                .SingleAsync(x => x.Id == scenario.ReferrerUserId);
            directReferrer.Referrer = indirectReferrer;
            await directRelationship.SaveChangesAsync();
        }

        await using (var preview = new eLottoContext(options))
        {
            var result = await new SorteosRepository(preview).VerifyWinningNumberAsync(
                scenario.SorteoId,
                "42",
                CancellationToken.None);
            True(result.HayGanador, "El preview debe identificar al ganador vendido.");
            Equal("Referidor del ganador", result.Referencia.ReferidorNombre);
            True(!result.Referencia.Finalizado, "El preview no debe presentarse como premio persistido.");
            Equal(true, result.Referencia.PremioGenerado);
            Equal(4, result.Referencia.BoletosRequeridos);
            Equal(4, result.Referencia.BoletosConfirmados);
            Equal(5000m, result.Referencia.ImportePremio);
            Equal(0, await preview.ReferralWinnerCashRewards.CountAsync());
        }

        var finalizationBefore = ApplicationClock.NowOffset;
        await using (var finalization = new eLottoContext(options))
        {
            var result = await new SorteosRepository(finalization).FinalizeWinnerAsync(
                scenario.SorteoId,
                "42",
                CancellationToken.None);
            Equal(SorteoResultadoStatus.Ok, result.Status);
            Equal("Referidor del ganador", result.Ganador.Referencia.ReferidorNombre);
            True(result.Ganador.Referencia.Finalizado, "El resultado devuelto debe ser persistido.");
            Equal(true, result.Ganador.Referencia.PremioGenerado);
            Equal(4, result.Ganador.Referencia.BoletosRequeridos);
            Equal(4, result.Ganador.Referencia.BoletosConfirmados);
            Equal(5000m, result.Ganador.Referencia.ImportePremio);
            Equal(ReferralWinnerCashRewardStatus.Paid, result.Ganador.Referencia.Estado);
            True(result.Ganador.Referencia.FechaPago.HasValue,
                "El premio debe quedar entregado en la misma finalización.");
        }
        var finalizationAfter = ApplicationClock.NowOffset;

        await UpdateWinnerReferralSettingsAsync(options, true, 10000m, 200);
        await using (var historicalSnapshot = new eLottoContext(options))
        {
            var result = await new SorteosRepository(historicalSnapshot)
                .GetLatestFinalizedWinnerAsync(CancellationToken.None);
            Equal(4, result.Referencia.BoletosRequeridos);
            Equal(4, result.Referencia.BoletosConfirmados);
            Equal(5000m, result.Referencia.ImportePremio);
            Equal(ReferralWinnerCashRewardStatus.Paid, result.Referencia.Estado);
            True(result.Referencia.FechaPago.HasValue,
                "El histórico debe conservar la fecha de entrega original.");
        }

        await using (var paidVerification = new eLottoContext(options))
        {
            var result = await new SorteosRepository(paidVerification)
                .GetLatestFinalizedWinnerAsync(CancellationToken.None);
            Equal(ReferralWinnerCashRewardStatus.Paid, result.Referencia.Estado);
            True(result.Referencia.FechaPago.HasValue,
                "La consulta final debe devolver la fecha de entrega.");
            Equal("Referidor del ganador", result.Referencia.ReferidorNombre);
        }

        await UpdateWinnerReferralSettingsAsync(options, true, 5000m, 4);

        await using (var verification = new eLottoContext(options))
        {
            var reward = await verification.ReferralWinnerCashRewards
                .AsNoTracking()
                .Include(x => x.WinnerRecord)
                .SingleAsync();
            Equal(scenario.ReferrerUserId, reward.ReferrerUserId);
            Equal(4, reward.RequiredTickets);
            Equal(4, reward.ActualTickets);
            Equal(5000m, reward.RewardAmount);
            Equal(ReferralWinnerCashRewardStatus.Paid, reward.Status);
            True(reward.PaidAt.HasValue,
                "El premio finalizado debe persistir la fecha de entrega.");
            var lottery = await verification.Sorteos.AsNoTracking()
                .SingleAsync(x => x.Id == scenario.SorteoId);
            var zoneBefore = timeService.ConvertToSorteoTime(finalizationBefore, lottery).DateTime;
            var zoneAfter = timeService.ConvertToSorteoTime(finalizationAfter, lottery).DateTime;
            True(reward.CreatedAt >= zoneBefore && reward.CreatedAt <= zoneAfter,
                "El premio referido debe fecharse en la zona del sorteo.");
            Equal(reward.WinnerRecord.FechaFin, reward.CreatedAt);
            Equal(reward.CreatedAt, reward.PaidAt);
            Equal(scenario.WinnerUserId, reward.WinnerRecord.UsuarioIdGanador);
            Equal(scenario.SorteoId, reward.WinnerRecord.SorteoId);
            Equal(reward.Id, (await verification.GanadoresSorteos
                .Include(x => x.ReferralWinnerCashReward)
                .SingleAsync()).ReferralWinnerCashReward.Id);
            Equal(0, await verification.WalletTransactions.CountAsync(
                x => x.Type == WalletTransactionType.ReferralDepositReward ||
                     x.Amount == 5000m));
        }

        await using (var duplicate = new eLottoContext(options))
        {
            var repeated = await new SorteosRepository(duplicate).FinalizeWinnerAsync(
                scenario.SorteoId,
                "42",
                CancellationToken.None);
            Equal(SorteoResultadoStatus.SorteoFinalizado, repeated.Status);
        }

        var secondLotteryId = await AddLotteryForReferralWinnerAsync(
            options,
            scenario.WinnerUserId,
            scenario.ReferrerUserId,
            actualReferrerTickets: 4,
            name: "Segundo sorteo ganado");
        await using (var secondFinalization = new eLottoContext(options))
        {
            var result = await new SorteosRepository(secondFinalization).FinalizeWinnerAsync(
                secondLotteryId,
                "42",
                CancellationToken.None);
            Equal(SorteoResultadoStatus.Ok, result.Status);
        }

        await using var finalVerification = new eLottoContext(options);
        Equal(2, await finalVerification.ReferralWinnerCashRewards.CountAsync());
        Equal(0, await finalVerification.ReferralWinnerCashRewards.CountAsync(
            x => x.ReferrerUserId == scenario.ReferrerUserId &&
                 x.Status == ReferralWinnerCashRewardStatus.Pending));
        Equal(2, await finalVerification.ReferralWinnerCashRewards.CountAsync(
            x => x.ReferrerUserId == scenario.ReferrerUserId &&
                 x.Status == ReferralWinnerCashRewardStatus.Paid));
        Equal(0, await finalVerification.ReferralWinnerCashRewards.CountAsync(
            x => x.ReferrerUserId != scenario.ReferrerUserId));

        var prizeHistory = await new UserPrizeHistoryService(finalVerification, new SorteoTimeService())
            .GetAsync(scenario.ReferrerUserId, CancellationToken.None);
        Equal(2, prizeHistory.TotalReferidos);
        Equal(10000m, prizeHistory.ImporteReferidos);
        Equal(2, prizeHistory.Premios.Count(x => x.Tipo == "referido"));
        True(prizeHistory.Premios.Where(x => x.Tipo == "referido")
            .All(x => x.ImportePremio == 5000m),
            "¿He ganado? debe usar el importe histórico de cada premio por referido.");
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}

static async Task TestReferralWinnerCashRewardEligibilityAsync()
{
    var options = CreateOptions($"eLotto_ReferralWinnerEligibility_{Guid.NewGuid():N}");
    try
    {
        var scenario = await CreateReferralWinnerScenarioAsync(
            options,
            minimumTickets: 5,
            actualTickets: 5,
            rewardAmount: 5000m,
            isActive: false,
            otherLotteryTickets: 10);
        await FinalizeLotteryAsync(options, scenario.SorteoId);
        await AssertWinnerCashRewardCountAsync(options, 0);

        await UpdateWinnerReferralSettingsAsync(options, true, 0m, 5);
        var zeroRewardLottery = await AddLotteryForReferralWinnerAsync(
            options, scenario.WinnerUserId, scenario.ReferrerUserId, 5, "Premio configurado en cero");
        await FinalizeLotteryAsync(options, zeroRewardLottery);
        await AssertWinnerCashRewardCountAsync(options, 0);

        await UpdateWinnerReferralSettingsAsync(options, true, 5000m, 5);
        var insufficientLottery = await AddLotteryForReferralWinnerAsync(
            options, scenario.WinnerUserId, scenario.ReferrerUserId, 4, "Participación insuficiente");
        await using (var insufficientPreview = new eLottoContext(options))
        {
            var preview = await new SorteosRepository(insufficientPreview)
                .VerifyWinningNumberAsync(insufficientLottery, "42", CancellationToken.None);
            Equal(false, preview.Referencia.PremioGenerado);
            Equal(5, preview.Referencia.BoletosRequeridos);
            Equal(4, preview.Referencia.BoletosConfirmados);
            True(!preview.Referencia.ImportePremio.HasValue,
                "Un preview no elegible no debe mostrar un premio potencial.");
            Equal(0, await insufficientPreview.ReferralWinnerCashRewards.CountAsync());
        }
        await FinalizeLotteryAsync(options, insufficientLottery);
        await AssertWinnerCashRewardCountAsync(options, 0);
        await using (var noRewardVerification = new eLottoContext(options))
        {
            var result = await new SorteosRepository(noRewardVerification)
                .GetLatestFinalizedWinnerAsync(CancellationToken.None);
            Equal("Referidor del ganador", result.Referencia.ReferidorNombre);
            Equal(false, result.Referencia.PremioGenerado);
            True(!result.Referencia.BoletosRequeridos.HasValue,
                "Sin premio persistido no deben inventarse snapshots históricos.");
            True(!result.Referencia.BoletosConfirmados.HasValue,
                "Sin premio persistido no deben inventarse boletos históricos.");
        }

        await UpdateWinnerReferralSettingsAsync(options, true, 3000m, 0);
        var zeroMinimumLottery = await AddLotteryForReferralWinnerAsync(
            options, scenario.WinnerUserId, scenario.ReferrerUserId, 0, "Mínimo cero");
        await FinalizeLotteryAsync(options, zeroMinimumLottery);

        await using var verification = new eLottoContext(options);
        var reward = await verification.ReferralWinnerCashRewards.AsNoTracking().SingleAsync();
        Equal(0, reward.RequiredTickets);
        Equal(0, reward.ActualTickets);
        Equal(3000m, reward.RewardAmount);
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}

static async Task TestReferralWinnerCashRewardRollbackAsync()
{
    var options = CreateOptions($"eLotto_ReferralWinnerRollback_{Guid.NewGuid():N}");
    try
    {
        var scenario = await CreateReferralWinnerScenarioAsync(
            options,
            minimumTickets: 2,
            actualTickets: 2,
            rewardAmount: 5000m,
            includeSettings: false);

        await using (var failedFinalization = new eLottoContext(options))
        {
            await ThrowsAsync<InvalidOperationException>(() =>
                new SorteosRepository(failedFinalization).FinalizeWinnerAsync(
                    scenario.SorteoId,
                    "42",
                    CancellationToken.None));
        }

        await using (var rolledBack = new eLottoContext(options))
        {
            Equal(0, await rolledBack.GanadoresSorteos.CountAsync());
            Equal(0, await rolledBack.ReferralWinnerCashRewards.CountAsync());
            True(
                await rolledBack.Sorteos
                    .Where(x => x.Id == scenario.SorteoId)
                    .Select(x => x.NumeroGanador)
                    .SingleAsync() == null,
                "El sorteo debe continuar sin finalizar después del rollback.");
            Equal(3, await rolledBack.BoletosConfirmados.CountAsync(
                x => x.SorteosId == scenario.SorteoId));
        }

        await using (var repair = new eLottoContext(options))
        {
            repair.ReferralProgramSettings.Add(CreateReferralSettings(
                true,
                5000m,
                2));
            await repair.SaveChangesAsync();
        }

        await FinalizeLotteryAsync(options, scenario.SorteoId);
        await using var verification = new eLottoContext(options);
        Equal(1, await verification.GanadoresSorteos.CountAsync());
        Equal(1, await verification.ReferralWinnerCashRewards.CountAsync());
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}

static List<ValidationResult> Validate(object instance)
{
    var results = new List<ValidationResult>();
    Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);
    return results;
}

static ReferralProgramSettings SettingsValues(
    bool isActive,
    decimal percentage,
    int maximumDeposits,
    decimal winnerReward,
    int minimumTickets) =>
    new()
    {
        Id = 1,
        IsActive = isActive,
        DepositRewardPercentage = percentage,
        MaxRewardedDeposits = maximumDeposits,
        WinnerCashRewardAmount = winnerReward,
        MinimumConfirmedTickets = minimumTickets
    };

static Task TestReferralCodeGenerationAsync()
{
    var generatedCodes = Enumerable.Range(0, 512)
        .Select(_ => ReferralCodeGenerator.Create())
        .ToArray();

    True(
        generatedCodes.All(ReferralCodeGenerator.IsValid),
        "Todos los códigos deben tener exactamente ocho caracteres A-Z0-9.");
    Equal(
        generatedCodes.Length,
        generatedCodes.Distinct(StringComparer.Ordinal).Count(),
        "La muestra criptográficamente aleatoria no debe contener duplicados.");

    return Task.CompletedTask;
}

static async Task TestReferralCodeCollisionRetryAsync()
{
    var options = CreateOptions($"eLotto_ReferralCollision_{Guid.NewGuid():N}");
    try
    {
        await using var context = new eLottoContext(options);
        await context.Database.EnsureCreatedAsync();

        context.Rol.Add(new Rol { Name = "User" });
        context.Users.Add(new Users
        {
            Name = "Código existente",
            Email = "existing-referral@example.test",
            WhatsApp = "5216621000001",
            User = "existingref",
            Password = "not-used",
            IsActive = true,
            Date = ApplicationClock.Now,
            Languaje = "es",
            ReferralCode = "AAAAAAAA"
        });
        await context.SaveChangesAsync();

        var generator = new SequentialReferralCodeGenerator("AAAAAAAA", "BBBBBBBB");
        var repository = new UserRepository(context, generator);
        var newUser = new Users
        {
            Name = "Código reintentado",
            Email = "retried-referral@example.test",
            WhatsApp = "5216621000002",
            User = "retriedref",
            Password = "not-used",
            IsActive = true,
            Date = ApplicationClock.Now,
            Languaje = "es",
            ReferralCode = null
        };

        True(
            await repository.CreateWithRoleAsync(newUser, "User", ApplicationClock.Now.AddYears(1)),
            "El registro debe completarse después de regenerar el código en colisión.");
        Equal("BBBBBBBB", newUser.ReferralCode);
        Equal(2, await context.Users.CountAsync());
        Equal(1, await context.UserRols.CountAsync());
        Equal(2, generator.Calls);
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}

static async Task TestReferralMigrationAsync()
{
    const string PreviousMigration = "20260922191356_ActiveUserSession";
    var options = CreateOptions($"eLotto_ReferralMigration_{Guid.NewGuid():N}");
    try
    {
        await using var context = new eLottoContext(options);
        await context.Database.EnsureDeletedAsync();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);

        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO [Users]
                ([Name], [Email], [WhatsApp], [User], [Password], [IsActive], [ConfirmCode],
                 [Date], [Languaje], [IsDark], [IsAndroid], [IsIos], [StripeCustomerId], [ActiveSessionId])
            VALUES
                (N'Existente uno', N'legacy-one@example.test', N'5216622000001', N'legacyone', N'not-used', 1, N'',
                 SYSDATETIME(), N'es', 0, 0, 0, NULL, NULL),
                (N'Existente dos', N'legacy-two@example.test', N'5216622000002', N'legacytwo', N'not-used', 1, N'',
                 SYSDATETIME(), N'es', 0, 0, 0, NULL, NULL);
            """);

        await migrator.MigrateAsync();
        context.ChangeTracker.Clear();

        var codes = await context.Users.AsNoTracking()
            .OrderBy(user => user.Id)
            .Select(user => user.ReferralCode)
            .ToListAsync();
        Equal(2, codes.Count);
        True(codes.All(ReferralCodeGenerator.IsValid), "El backfill debe producir códigos A-Z0-9 válidos.");
        Equal(2, codes.Distinct(StringComparer.Ordinal).Count(), "El backfill debe producir códigos únicos.");

        var settings = await context.ReferralProgramSettings.AsNoTracking().SingleAsync();
        Equal((byte)1, settings.Id);
        True(settings.IsActive, "La configuración inicial debe quedar activa.");
        Equal(10m, settings.DepositRewardPercentage);
        Equal(5, settings.MaxRewardedDeposits);
        Equal(5000m, settings.WinnerCashRewardAmount);
        Equal(100, settings.MinimumConfirmedTickets);
        Equal(8, settings.RowVersion.Length, "SQL Server debe generar un rowversion de ocho bytes.");

        Equal(1, await ScalarAsync(context,
            "SELECT COUNT(*) FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[Users]') AND [name] = N'UX_Users_ReferralCode' AND [is_unique] = 1"));
        Equal(1, await ScalarAsync(context,
            "SELECT COUNT(*) FROM sys.check_constraints WHERE [parent_object_id] = OBJECT_ID(N'[Users]') AND [name] = N'CK_Users_ReferralCode_Format'"));
        Equal(1, await ScalarAsync(context,
            "SELECT COUNT(*) FROM sys.foreign_keys WHERE [parent_object_id] = OBJECT_ID(N'[Users]') AND [name] = N'FK_Users_Users_ReferredByUserId' AND [delete_referential_action] = 0"));
        Equal(1, await ScalarAsync(context,
            "SELECT COUNT(*) FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[ReferralDepositRewards]') AND [name] = N'UX_ReferralDepositRewards_SourceDepositTransactionId' AND [is_unique] = 1"));
        Equal(1, await ScalarAsync(context,
            "SELECT COUNT(*) FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[ReferralDepositRewards]') AND [name] = N'UX_ReferralDepositRewards_RewardWalletTransactionId' AND [is_unique] = 1"));
        Equal(1, await ScalarAsync(context,
            "SELECT COUNT(*) FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[ReferralWinnerCashRewards]') AND [name] = N'UX_ReferralWinnerCashRewards_WinnerRecordId' AND [is_unique] = 1"));

        await migrator.MigrateAsync(PreviousMigration);
        Equal(0, await ScalarAsync(context,
            "SELECT COUNT(*) FROM sys.tables WHERE [name] IN (N'ReferralProgramSettings', N'ReferralDepositRewards', N'ReferralWinnerCashRewards')"));
        Equal(0, await ScalarAsync(context,
            "SELECT COUNT(*) FROM sys.columns WHERE [object_id] = OBJECT_ID(N'[Users]') AND [name] IN (N'ReferralCode', N'ReferredByUserId')"));
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}

static async Task TestReferralWinnerRelationshipAsync()
{
    var options = CreateOptions($"eLotto_ReferralWinnerRelation_{Guid.NewGuid():N}");
    try
    {
        await using var context = new eLottoContext(options);
        await context.Database.EnsureCreatedAsync();

        var referrer = new Users
        {
            Name = "Referidor",
            Email = "winner-referrer@example.test",
            WhatsApp = "5216623000001",
            User = "winnerref",
            Password = "not-used",
            IsActive = true,
            Date = ApplicationClock.Now,
            Languaje = "es"
        };
        context.Users.Add(referrer);
        await context.SaveChangesAsync();

        var winner = new GanadoresSorteos
        {
            NumeroGanador = "000001",
            FolioSorteo = "REFERRAL-WINNER-TEST",
            Nombre = "Sorteo prueba",
            FechaFin = ApplicationClock.Now,
            WhatsAppGanador = "5216623000002",
            UsuarioIdGanador = 999,
            NombreGanador = "Ganador prueba",
            ReferralWinnerCashReward = new ReferralWinnerCashReward
            {
                ReferrerUserId = referrer.Id,
                RequiredTickets = 100,
                ActualTickets = 125,
                RewardAmount = 5000m,
                CreatedAt = ApplicationClock.Now
            }
        };

        context.GanadoresSorteos.Add(winner);
        await context.SaveChangesAsync();

        True(winner.Id > 0, "EF debe generar el Id del registro ganador.");
        Equal(winner.Id, winner.ReferralWinnerCashReward.WinnerRecordId);
        Equal(ReferralWinnerCashRewardStatus.Pending, winner.ReferralWinnerCashReward.Status);
        Equal(1, await context.ReferralWinnerCashRewards.CountAsync());

        var winnerForeignKey = context.Model.FindEntityType(typeof(ReferralWinnerCashReward))!
            .GetForeignKeys()
            .Single(foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(GanadoresSorteos));
        True(winnerForeignKey.IsUnique, "La relación del ganador debe ser uno a cero/uno.");
        Equal(DeleteBehavior.Restrict, winnerForeignKey.DeleteBehavior);

        var userForeignKey = context.Model.FindEntityType(typeof(Users))!
            .GetForeignKeys()
            .Single(foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(Users));
        Equal(DeleteBehavior.Restrict, userForeignKey.DeleteBehavior);

        var referralForeignKeys = new[]
        {
            typeof(ReferralDepositReward),
            typeof(ReferralWinnerCashReward)
        }
            .SelectMany(type => context.Model.FindEntityType(type)!.GetForeignKeys());
        True(
            referralForeignKeys.All(foreignKey => foreignKey.DeleteBehavior == DeleteBehavior.Restrict),
            "Todas las FK del historial de referidos deben usar DeleteBehavior.Restrict.");

        var rowVersion = context.Model.FindEntityType(typeof(ReferralProgramSettings))!
            .FindProperty(nameof(ReferralProgramSettings.RowVersion))!;
        True(rowVersion.IsConcurrencyToken, "RowVersion debe ser token de concurrencia.");
        True(!rowVersion.IsNullable, "RowVersion debe ser obligatorio.");
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}

static async Task TestNormalRegistrationReferralAsync()
{
    var options = CreateOptions($"eLotto_NormalRegistration_{Guid.NewGuid():N}");
    try
    {
        await using var context = new eLottoContext(options);
        await context.Database.EnsureCreatedAsync();
        context.Rol.Add(new Rol { Name = "User" });
        await context.SaveChangesAsync();

        var result = await CreateAccountService(context).CreateUserAsync(
            RegistrationRequest("normaluser", "5216624000001"));

        True(ResponseOk(result), ResponseMessage(result));
        var created = await context.Users.AsNoTracking().SingleAsync(user => user.User == "normaluser");
        True(created.ReferredByUserId == null, "El registro normal no debe asignar referidor.");
        True(ReferralCodeGenerator.IsValid(created.ReferralCode), "El nuevo usuario debe recibir su código propio.");
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}

static async Task TestAdultConfirmationRegistrationAsync()
{
    var options = CreateOptions($"eLotto_AdultConfirmation_{Guid.NewGuid():N}");
    try
    {
        await using var context = new eLottoContext(options);
        await context.Database.EnsureCreatedAsync();
        context.Rol.Add(new Rol { Name = "User" });
        await context.SaveChangesAsync();

        var service = CreateAccountService(context);
        var rejectedRequest = RegistrationRequest("underage01", "5216624050001");
        rejectedRequest.ConfirmedOver18 = false;

        var rejected = await service.CreateUserAsync(rejectedRequest);

        True(!ResponseOk(rejected), "El backend debe rechazar la confirmación ausente.");
        Equal(
            "Debes confirmar que eres mayor de 18 años para registrarte.",
            ResponseMessage(rejected));
        Equal(0, await context.Users.CountAsync());

        var beforeConfirmation = ApplicationClock.Now;
        var accepted = await service.CreateUserAsync(
            RegistrationRequest("adultuser", "5216624050002"));
        var afterConfirmation = ApplicationClock.Now;

        True(ResponseOk(accepted), ResponseMessage(accepted));
        var created = await context.Users.AsNoTracking()
            .SingleAsync(user => user.User == "adultuser");
        True(
            created.Over18ConfirmedAt >= beforeConfirmation &&
            created.Over18ConfirmedAt <= afterConfirmation,
            "Debe conservarse la hora local del servidor al confirmar.");
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}

static async Task TestReferredRegistrationNormalizationAsync()
{
    var options = CreateOptions($"eLotto_ReferralNormalization_{Guid.NewGuid():N}");
    try
    {
        await using var context = new eLottoContext(options);
        await context.Database.EnsureCreatedAsync();
        context.Rol.Add(new Rol { Name = "User" });
        var owner = ExistingUser("refowner", "5216624100000", "ABC12345");
        context.Users.Add(owner);
        await context.SaveChangesAsync();

        var service = CreateAccountService(context);
        var cases = new[]
        {
            (Username: "lowercase1", WhatsApp: "5216624100001", Code: "abc12345"),
            (Username: "uppercase1", WhatsApp: "5216624100002", Code: "ABC12345"),
            (Username: "trimmed001", WhatsApp: "5216624100003", Code: " ABC12345 ")
        };

        foreach (var testCase in cases)
        {
            var result = await service.CreateUserAsync(
                RegistrationRequest(testCase.Username, testCase.WhatsApp, testCase.Code));
            True(ResponseOk(result), ResponseMessage(result));
        }

        var referredUsers = await context.Users.AsNoTracking()
            .Where(user => user.Id != owner.Id)
            .ToListAsync();
        Equal(3, referredUsers.Count);
        True(
            referredUsers.All(user => user.ReferredByUserId == owner.Id),
            "Todas las variantes normalizadas deben resolver al mismo propietario.");
        True(
            referredUsers.All(user => ReferralCodeGenerator.IsValid(user.ReferralCode)),
            "Cada usuario referido debe tener un código propio válido.");
        True(
            referredUsers.All(user => user.ReferralCode != owner.ReferralCode),
            "El código propio no debe reutilizar el código recibido.");
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}

static async Task TestInvalidReferralRegistrationAsync()
{
    var options = CreateOptions($"eLotto_InvalidReferral_{Guid.NewGuid():N}");
    try
    {
        await using var context = new eLottoContext(options);
        await context.Database.EnsureCreatedAsync();
        context.Rol.Add(new Rol { Name = "User" });
        await context.SaveChangesAsync();
        var service = CreateAccountService(context);

        var invalidCodes = new[] { "ABC", "ABCDEFGHI", "ABC12-45" };
        for (var index = 0; index < invalidCodes.Length; index++)
        {
            var result = await service.CreateUserAsync(RegistrationRequest(
                $"invalid{index:D2}",
                $"521662420000{index}",
                invalidCodes[index]));
            True(!ResponseOk(result), "Un código con formato inválido debe rechazarse.");
            Equal("El código de referido no tiene un formato válido.", ResponseMessage(result));
        }

        var missingResult = await service.CreateUserAsync(
            RegistrationRequest("missing01", "5216624200010", "ZZZZZZZZ"));
        True(!ResponseOk(missingResult), "Un código válido inexistente debe rechazarse.");
        Equal("El código de referido no existe.", ResponseMessage(missingResult));
        Equal(0, await context.Users.CountAsync());
        Equal(0, await context.UserRols.CountAsync());
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}

static async Task TestInactiveReferralProgramAttributionAsync()
{
    var options = CreateOptions($"eLotto_InactiveReferral_{Guid.NewGuid():N}");
    try
    {
        await using var context = new eLottoContext(options);
        await context.Database.EnsureCreatedAsync();
        context.Rol.Add(new Rol { Name = "User" });
        var owner = ExistingUser("inactiveowner", "5216624300000", "INACTIVE");
        context.Users.Add(owner);
        context.ReferralProgramSettings.Add(new ReferralProgramSettings
        {
            Id = 1,
            IsActive = false,
            DepositRewardPercentage = 10m,
            MaxRewardedDeposits = 5,
            WinnerCashRewardAmount = 5000m,
            MinimumConfirmedTickets = 100,
            UpdatedAt = ApplicationClock.Now
        });
        await context.SaveChangesAsync();

        var result = await CreateAccountService(context).CreateUserAsync(
            RegistrationRequest("inactive1", "5216624300001", owner.ReferralCode));

        True(ResponseOk(result), ResponseMessage(result));
        var created = await context.Users.AsNoTracking().SingleAsync(user => user.User == "inactive1");
        Equal(owner.Id, created.ReferredByUserId);
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}

static async Task TestDirectReferralAttributionAsync()
{
    var options = CreateOptions($"eLotto_DirectReferral_{Guid.NewGuid():N}");
    try
    {
        await using var context = new eLottoContext(options);
        await context.Database.EnsureCreatedAsync();
        context.Rol.Add(new Rol { Name = "User" });
        var userA = ExistingUser("directusera", "5216624400000", "DIRECTA1");
        context.Users.Add(userA);
        await context.SaveChangesAsync();
        var service = CreateAccountService(context);

        var resultB = await service.CreateUserAsync(
            RegistrationRequest("directuserb", "5216624400001", userA.ReferralCode));
        True(ResponseOk(resultB), ResponseMessage(resultB));
        var userB = await context.Users.AsNoTracking().SingleAsync(user => user.User == "directuserb");

        var resultC = await service.CreateUserAsync(
            RegistrationRequest("directuserc", "5216624400002", userB.ReferralCode));
        True(ResponseOk(resultC), ResponseMessage(resultC));
        var userC = await context.Users.AsNoTracking().SingleAsync(user => user.User == "directuserc");

        Equal(userA.Id, userB.ReferredByUserId);
        Equal(userB.Id, userC.ReferredByUserId);
        True(userC.ReferredByUserId != userA.Id, "C no debe quedar atribuido directamente a A.");
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}

static async Task TestReferralRegistrationAtomicityAsync()
{
    var options = CreateOptions($"eLotto_ReferralAtomicity_{Guid.NewGuid():N}");
    try
    {
        await using var context = new eLottoContext(options);
        await context.Database.EnsureCreatedAsync();
        var owner = ExistingUser("atomicowner", "5216624500000", "ATOMIC01");
        context.Users.Add(owner);
        await context.SaveChangesAsync();

        var result = await CreateAccountService(context).CreateUserAsync(
            RegistrationRequest("atomicnew", "5216624500001", owner.ReferralCode));

        True(!ResponseOk(result), "El registro debe fallar si no existe el rol User.");
        Equal(1, await context.Users.CountAsync());
        Equal(0, await context.UserRols.CountAsync());
        True(
            !await context.Users.AnyAsync(user => user.User == "atomicnew"),
            "No debe quedar un usuario parcial con la atribución resuelta.");
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}

static Task TestReferralAttributionImmutabilityAsync()
{
    True(
        typeof(UserCreate).GetProperty(nameof(Users.ReferredByUserId)) == null,
        "El request público no debe aceptar ReferredByUserId.");
    Equal(
        1,
        typeof(UserCreate).GetProperties()
            .Count(property => property.Name.Contains("Referral", StringComparison.Ordinal)));
    return Task.CompletedTask;
}

static AccountServices CreateAccountService(eLottoContext context, IWhatsAppService whatsAppService = null)
{
    var configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string>
        {
            ["Branding:ApplicationName"] = "eLotto Tests",
            ["Authentication:UserRoleValidityYears"] = "10"
        })
        .Build();

    return new AccountServices(
        new UserRepository(context),
        whatsAppService ?? new NullWhatsAppService(),
        configuration);
}

static async Task TestPendingAccountPasswordResetAsync()
{
    var options = CreateOptions($"eLotto_PendingPasswordReset_{Guid.NewGuid():N}");
    try
    {
        await using var context = new eLottoContext(options);
        await context.Database.EnsureCreatedAsync();
        var user = new Users
        {
            Name = "Usuario pendiente",
            WhatsApp = "5216625000001",
            User = "pendingreset",
            Password = BCrypt.Net.BCrypt.HashPassword("PreviousPassword1!"),
            IsActive = false,
            ConfirmCode = string.Empty,
            Date = ApplicationClock.Now,
            Languaje = "es"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var service = CreateAccountService(context, new SuccessfulWhatsAppService());
        var sent = await service.SendResetPinAsync("6625000001");
        True(ResponseOk(sent), ResponseMessage(sent));
        var pin = user.ConfirmCode;
        True(pin.Length == 6 && pin.All(char.IsDigit), "Debe emitirse un PIN de recuperación de seis dígitos.");

        var request = new NewPassword
        {
            WhatsApp = "6625000001",
            Password = "NewPassword1!",
            ConfirmPassword = "NewPassword1!",
            ConfirmCode = "000000"
        };
        True(!ResponseOk(await service.ResetPasswordAsync(request)), "Un PIN incorrecto no debe cambiar la cuenta.");
        True(!user.IsActive, "La cuenta debe seguir pendiente tras un PIN incorrecto.");
        True(BCrypt.Net.BCrypt.Verify("PreviousPassword1!", user.Password), "El password anterior debe conservarse.");

        request.ConfirmCode = string.Empty;
        True(!ResponseOk(await service.ResetPasswordAsync(request)), "Un PIN vacío no debe cambiar la cuenta.");
        True(!user.IsActive, "Un PIN vacío no debe confirmar la cuenta.");

        request.ConfirmCode = pin;
        True(ResponseOk(await service.ResetPasswordAsync(request)), "El PIN correcto debe permitir recuperar el password.");
        True(user.IsActive, "La recuperación exitosa debe confirmar la cuenta.");
        True(BCrypt.Net.BCrypt.Verify("NewPassword1!", user.Password), "Debe guardarse el nuevo password.");
        Equal(string.Empty, user.ConfirmCode);
        True(!ResponseOk(await service.ResetPasswordAsync(request)), "El PIN usado no debe poder reutilizarse.");
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}

static async Task TestOptionalImageTopicsAsync()
{
    var options = CreateOptions($"eLotto_ImageTopics_{Guid.NewGuid():N}");
    try
    {
        var seeded = await RecreateAndSeedAsync(options, 1);
        await using var context = new eLottoContext(options);
        var sorteo = await context.Sorteos.SingleAsync(x => x.Id == seeded.SorteoId);
        sorteo.Imagen2 = "/images/rascaditos.webp";
        sorteo.Imagen2Tema = "rascaditos";
        sorteo.Imagen3 = "/images/referidos.webp";
        sorteo.Imagen3Tema = "referidos";
        await context.SaveChangesAsync();

        var saved = await context.Sorteos.AsNoTracking().SingleAsync(x => x.Id == seeded.SorteoId);
        Equal("rascaditos", saved.Imagen2Tema);
        Equal("referidos", saved.Imagen3Tema);

        True(await new SorteosRepository(context).RemoveOptionalImageAsync(saved.Id, 2),
            "La imagen 2 debe poder eliminarse.");
        var updated = await context.Sorteos.AsNoTracking().SingleAsync(x => x.Id == seeded.SorteoId);
        True(updated.Imagen2 == null && updated.Imagen2Tema == null,
            "Eliminar la imagen debe limpiar su tema.");
        Equal("referidos", updated.Imagen3Tema);
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}

static async Task TestOptionalImageTopicValidationAsync()
{
    using var stream = new MemoryStream(new byte[] { 1 });
    var image = new FormFile(stream, 0, 1, "Imagen", "test.webp");

    foreach (var theme in new string[] { null, "otro" })
    {
        var controller = new SorteosController(null, null, null);
        var result = await controller.Create(new SorteoFormDto
        {
            Nombre = "Sorteo de prueba",
            Imagen1 = image,
            Imagen2 = image,
            Imagen2Tema = theme
        }, CancellationToken.None);

        True(result is ObjectResult { Value: ValidationProblemDetails },
            "Una imagen secundaria nueva requiere un tema válido.");
        True(controller.ModelState.ContainsKey(nameof(SorteoFormDto.Imagen2Tema)),
            "El error debe señalar el tema de la imagen 2.");
    }

    var withoutImage = new SorteosController(null, null, null);
    var withoutImageResult = await withoutImage.Create(new SorteoFormDto
    {
        Nombre = "Sorteo de prueba",
        Imagen1 = image,
        Imagen3Tema = "referidos"
    }, CancellationToken.None);
    True(withoutImageResult is ObjectResult { Value: ValidationProblemDetails },
        "No debe admitirse un tema sin imagen secundaria.");
    True(withoutImage.ModelState.ContainsKey(nameof(SorteoFormDto.Imagen3Tema)),
        "El error debe señalar el tema de la imagen 3.");
}

static UserCreate RegistrationRequest(string username, string whatsApp, string referralCode = null) =>
    new()
    {
        User = username,
        Name = $"Usuario {username}",
        Password = "Password1!",
        ConfirmPassword = "Password1!",
        WhatsApp = whatsApp,
        Device = "web",
        ConfirmedOver18 = true,
        ReferralCode = referralCode
    };

static Users ExistingUser(string username, string whatsApp, string referralCode) =>
    new()
    {
        Name = $"Usuario {username}",
        Email = $"{username}@example.test",
        WhatsApp = whatsApp,
        User = username,
        Password = "not-used",
        IsActive = true,
        Date = ApplicationClock.Now,
        Languaje = "es",
        ReferralCode = referralCode
    };

static bool ResponseOk(object response) =>
    (bool)(response.GetType().GetProperty("Ok")?.GetValue(response)
        ?? throw new InvalidOperationException("La respuesta no contiene Ok."));

static string ResponseMessage(object response) =>
    (string)(response.GetType().GetProperty("Message")?.GetValue(response)
        ?? throw new InvalidOperationException("La respuesta no contiene Message."));

static async Task<int> ScalarAsync(eLottoContext context, string sql)
{
    var connection = context.Database.GetDbConnection();
    var shouldClose = connection.State != ConnectionState.Open;
    if (shouldClose)
        await connection.OpenAsync();

    try
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }
    finally
    {
        if (shouldClose)
            await connection.CloseAsync();
    }
}

static async Task TestSessionLogoutDoesNotRevokeNewLoginAsync()
{
    var options = CreateOptions($"eLotto_SessionTest_{Guid.NewGuid():N}");
    try
    {
        await using var context = new eLottoContext(options);
        await context.Database.EnsureCreatedAsync();
        var user = new Users
        {
            Name = "Usuario sesión",
            Email = "session@example.test",
            WhatsApp = "5216620000001",
            User = "sessiontest",
            Password = "not-used",
            IsActive = true,
            Date = ApplicationClock.Now,
            Languaje = "es"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var repository = new UserRepository(context);
        var previousSession = Guid.NewGuid();
        var currentSession = Guid.NewGuid();
        True(await repository.SetActiveSessionIdAsync(user.Id, previousSession), "No se inició la primera sesión.");
        True(await repository.SetActiveSessionIdAsync(user.Id, currentSession), "No se inició la segunda sesión.");
        True(!await repository.ClearActiveSessionIdAsync(user.Id, previousSession), "El logout anterior no debe revocar la sesión nueva.");
        True(await repository.GetActiveSessionIdAsync(user.Id) == currentSession, "La sesión nueva debe permanecer activa.");
        True(await repository.ClearActiveSessionIdAsync(user.Id, currentSession), "El logout vigente debe revocar su sesión.");
        True(await repository.GetActiveSessionIdAsync(user.Id) == null, "La sesión revocada debe quedar vacía.");
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}

static IConfiguration CreateTransparencyBrandingConfiguration() => new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string>
    {
        ["Branding:ApplicationName"] = "Sorteos Global Broker"
    })
    .Build();

static async Task TestTransparencyPublicationAsync()
{
    var options = CreateOptions($"eLottoTransparency_{Guid.NewGuid():N}");
    var branding = CreateTransparencyBrandingConfiguration();
    var directory = Path.Combine(Path.GetTempPath(), $"eLottoTransparency_{Guid.NewGuid():N}");
    var files = new SorteoTransparencyFileStore(directory);
    try
    {
        var seeded = await RecreateAndSeedAsync(options, 1);
        ISorteoTimeService timeService = new SorteoTimeService();
        await using (var setup = new eLottoContext(options))
        {
            var lottery = await setup.Sorteos.SingleAsync(x => x.Id == seeded.SorteoId);
            lottery.ZonaHoraria = "Cancun";
            lottery.Fecha = timeService.ConvertToSorteoTime(
                ApplicationClock.NowOffset.AddMinutes(100), lottery).DateTime;
            lottery.PorcentajeMinimoVenta = 1;
            setup.BoletosConfirmados.Add(new BoletosConfirmados
            {
                SorteosId = seeded.SorteoId, Numero = "042", FolioCompra = "VENTA01",
                UsuarioId = seeded.UserId, UsuarioIdConfirm = seeded.UserId,
                Fecha = ApplicationClock.Now.AddMinutes(-10)
            });
            await setup.SaveChangesAsync();
        }

        await using (var beforeClose = new eLottoContext(options))
        {
            var service = new SorteoTransparencyService(beforeClose,
                NullLogger<SorteoTransparencyService>.Instance, Options.Create(new LotteryRulesOptions()),
                timeService, files, branding);
            Equal("pendiente", (await service.GetStatusAsync(seeded.SorteoId, CancellationToken.None))!.Estado);
            var lottery = await beforeClose.Sorteos.SingleAsync(x => x.Id == seeded.SorteoId);
            True(!files.Exists(lottery), "Una solicitud anticipada no debe crear el archivo.");
            lottery.Fecha = timeService.ConvertToSorteoTime(
                ApplicationClock.NowOffset.AddMinutes(85), lottery).DateTime;
            await beforeClose.SaveChangesAsync();
            Equal("pendiente", (await service.GetStatusAsync(seeded.SorteoId, CancellationToken.None))!.Estado);
            True(await service.GetPdfAsync(seeded.SorteoId, CancellationToken.None) == null,
                "Durante los 15 minutos de tolerancia no debe existir PDF.");

            beforeClose.BoletosConfirmados.Add(new BoletosConfirmados
            {
                SorteosId = seeded.SorteoId, Numero = "043", FolioCompra = "VENTA02",
                UsuarioId = seeded.UserId, UsuarioIdConfirm = seeded.UserId,
                Fecha = ApplicationClock.Now.AddMinutes(-9)
            });
            lottery.Fecha = timeService.ConvertToSorteoTime(
                ApplicationClock.NowOffset.AddMinutes(74), lottery).DateTime;
            await beforeClose.SaveChangesAsync();
        }

        await using (var context = new eLottoContext(options))
        await using (var concurrentContext = new eLottoContext(options))
        {
            var service = new SorteoTransparencyService(context,
                NullLogger<SorteoTransparencyService>.Instance, Options.Create(new LotteryRulesOptions()),
                timeService, files, branding);
            var concurrentService = new SorteoTransparencyService(concurrentContext,
                NullLogger<SorteoTransparencyService>.Instance, Options.Create(new LotteryRulesOptions()),
                timeService, files, branding);
            var statuses = await Task.WhenAll(
                service.GetStatusAsync(seeded.SorteoId, CancellationToken.None),
                concurrentService.GetStatusAsync(seeded.SorteoId, CancellationToken.None));
            True(statuses.All(x => x?.Estado == "publicado"),
                "Las solicitudes simultáneas deben resolver el mismo documento publicado.");
            var lottery = await context.Sorteos.AsNoTracking().SingleAsync(x => x.Id == seeded.SorteoId);
            True(files.Exists(lottery), "El documento debe quedar en la ruta física.");
            Equal(1, Directory.GetFiles(directory, "*.pdf").Length);
            var first = await service.GetPdfAsync(seeded.SorteoId, CancellationToken.None);
            var second = await concurrentService.GetPdfAsync(seeded.SorteoId, CancellationToken.None);
            True(first != null && first.Value.Content.Length > 1000, "El PDF debe existir.");
            True(first!.Value.Content.SequenceEqual(second!.Value.Content),
                "Todos deben descargar exactamente los mismos bytes.");
            Equal(first.Value.Hash, Convert.ToHexString(SHA256.HashData(first.Value.Content)));
            True(first.Value.Content.SequenceEqual(await files.ReadAsync(lottery, CancellationToken.None)!),
                "La descarga debe entregar el archivo publicado.");
            await System.IO.File.WriteAllTextAsync(files.PathFor(lottery), "archivo alterado");
            var rejectedAlteredFile = false;
            try { await service.GetStatusAsync(seeded.SorteoId, CancellationToken.None); }
            catch (InvalidOperationException) { rejectedAlteredFile = true; }
            True(rejectedAlteredFile, "Un archivo alterado no debe presentarse como documento válido.");
        }

        await using (var reschedule = new eLottoContext(options))
        {
            var lottery = await reschedule.Sorteos.SingleAsync(x => x.Id == seeded.SorteoId);
            lottery.Fecha = lottery.Fecha.AddDays(7);
            await reschedule.SaveChangesAsync();
            var service = new SorteoTransparencyService(reschedule,
                NullLogger<SorteoTransparencyService>.Instance, Options.Create(new LotteryRulesOptions()),
                timeService, files, branding);
            True(await service.GetPdfAsync(seeded.SorteoId, CancellationToken.None) == null,
                "Un archivo de la fecha anterior no puede entregarse como vigente.");
        }
    }
    finally
    {
        await DeleteDatabaseAsync(options);
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}

static async Task TestResultPublishesTransparencyAsync()
{
    var options = CreateOptions($"eLottoResultTransparency_{Guid.NewGuid():N}");
    var branding = CreateTransparencyBrandingConfiguration();
    var directory = Path.Combine(Path.GetTempPath(), $"eLottoTransparency_{Guid.NewGuid():N}");
    var files = new SorteoTransparencyFileStore(directory);
    try
    {
        var seeded = await RecreateAndSeedAsync(options, 1);
        ISorteoTimeService timeService = new SorteoTimeService();
        await using var context = new eLottoContext(options);
        var lottery = await context.Sorteos.SingleAsync(x => x.Id == seeded.SorteoId);
        lottery.Fecha = timeService.ConvertToSorteoTime(
            ApplicationClock.NowOffset.AddMinutes(-5), lottery).DateTime;
        lottery.PorcentajeMinimoVenta = 1;
        context.BoletosConfirmados.Add(new BoletosConfirmados
        {
            SorteosId = seeded.SorteoId, Numero = "042", FolioCompra = "VENTA01",
            UsuarioId = seeded.UserId, UsuarioIdConfirm = seeded.UserId,
            Fecha = ApplicationClock.Now.AddMinutes(-10)
        });
        await context.SaveChangesAsync();

        var repository = new SorteosRepository(context, null, timeService, files);
        var blocked = await repository.VerifyWinningNumberAsync(
            seeded.SorteoId, "42", CancellationToken.None);
        Equal(SorteoResultadoStatus.DocumentoTransparenciaNoPublicado, blocked.Status);

        var transparency = new SorteoTransparencyService(context,
            NullLogger<SorteoTransparencyService>.Instance, Options.Create(new LotteryRulesOptions()),
            timeService, files, branding);
        var controller = new SorteosController(repository, null, transparency);
        var request = new VerificarNumeroGanadorRequest { NumeroGanador = "42" };
        True(await controller.VerifyWinningNumber(seeded.SorteoId, request, CancellationToken.None)
            is OkObjectResult, "La verificación debe publicar el PDF que faltaba.");
        True(files.Exists(lottery), "El resultado no puede continuar sin un PDF publicado.");
        var published = await files.ReadAsync(lottery, CancellationToken.None);

        True(await controller.FinalizeWinner(seeded.SorteoId, request, CancellationToken.None)
            is OkObjectResult, "La finalización debe reutilizar el documento publicado.");
        True(published!.SequenceEqual((await files.ReadAsync(lottery, CancellationToken.None))!),
            "La finalización no debe regenerar ni alterar el PDF.");
        Equal(1, Directory.GetFiles(directory, "*.pdf").Length);
    }
    finally
    {
        await DeleteDatabaseAsync(options);
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}

static async Task TestTransparencyFailClosedAsync()
{
    var branding = CreateTransparencyBrandingConfiguration();
    foreach (var (minutes, minimum, expected, invalidNumber, soldOut, prepublished) in new[]
    {
        (91, 70, "pendiente", false, false, false),
        (89, 70, "reprogramar", false, false, false),
        (89, 1, "pendiente", false, false, false),
        (68, 70, "reprogramar", false, false, false),
        (68, 1, "incidencia", true, false, false),
        (10, 1, "publicado", false, false, false),
        (10, 70, "reprogramar", false, false, true),
        (-1, 70, "reprogramar", false, false, true),
        (-1, 1, "no_aplica", false, true, false)
    })
    {
        var options = CreateOptions($"eLottoTransparencyClosed_{Guid.NewGuid():N}");
        var directory = Path.Combine(Path.GetTempPath(), $"eLottoTransparency_{Guid.NewGuid():N}");
        var files = new SorteoTransparencyFileStore(directory);
        try
        {
            var seeded = await RecreateAndSeedAsync(options, 1);
            ISorteoTimeService timeService = new SorteoTimeService();
            await using (var setup = new eLottoContext(options))
            {
                var lottery = await setup.Sorteos.SingleAsync(x => x.Id == seeded.SorteoId);
                lottery.ZonaHoraria = "Tijuana";
                lottery.Fecha = timeService.ConvertToSorteoTime(
                    ApplicationClock.NowOffset.AddMinutes(minutes), lottery).DateTime;
                lottery.PorcentajeMinimoVenta = minimum;
                var quantity = soldOut ? lottery.CantidadBoletos : 1;
                for (var index = 0; index < quantity; index++)
                    setup.BoletosConfirmados.Add(new BoletosConfirmados
                    {
                        SorteosId = seeded.SorteoId,
                        Numero = invalidNumber ? "999" : index.ToString("D3"),
                        FolioCompra = $"VENTA{index:D3}",
                        UsuarioId = seeded.UserId, UsuarioIdConfirm = seeded.UserId,
                        Fecha = ApplicationClock.Now.AddMinutes(-10)
                    });
                await setup.SaveChangesAsync();
                if (prepublished)
                    await files.WriteOnceAsync(lottery, "%PDF-test"u8.ToArray(), CancellationToken.None);
            }
            await using var context = new eLottoContext(options);
            var service = new SorteoTransparencyService(context,
                NullLogger<SorteoTransparencyService>.Instance, Options.Create(new LotteryRulesOptions()),
                timeService, files, branding);
            Equal(expected, (await service.GetStatusAsync(seeded.SorteoId, CancellationToken.None))!.Estado);
            var lotteryForFile = await context.Sorteos.AsNoTracking().SingleAsync(x => x.Id == seeded.SorteoId);
            Equal(prepublished || expected == "publicado", files.Exists(lotteryForFile));
            Equal(expected == "publicado", await service.GetPdfAsync(seeded.SorteoId, CancellationToken.None) != null);
            if (soldOut)
            {
                var verification = await new SorteosRepository(context, null, timeService, files)
                    .VerifyWinningNumberAsync(seeded.SorteoId, "000", CancellationToken.None);
                Equal(SorteoResultadoStatus.Ok, verification.Status);
            }
        }
        finally
        {
            await DeleteDatabaseAsync(options);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}

static Task TestTransparencyEndpointAuthorizationAsync()
{
    var controller = typeof(SorteoTransparencyController);
    Equal(true, controller.GetCustomAttribute<AuthorizeAttribute>() != null);
    Equal(true, controller.GetMethod(nameof(SorteoTransparencyController.GetStatus))!
        .GetCustomAttribute<AllowAnonymousAttribute>() != null);
    Equal(true, controller.GetMethod(nameof(SorteoTransparencyController.Download))!
        .GetCustomAttribute<AllowAnonymousAttribute>() != null);
    return Task.CompletedTask;
}

static Task TestTransparencyStoragePathAsync()
{
    var contentRoot = Path.Combine(Path.GetTempPath(), $"eLottoStorage_{Guid.NewGuid():N}");
    var webRoot = Path.Combine(contentRoot, "wwwroot");
    Equal(Path.Combine(contentRoot, "App_Data", "transparencia"),
        SorteoTransparencyFileStore.ResolveDirectory(contentRoot, webRoot, null));
    Equal(Path.Combine(contentRoot, "privado", "transparencia"),
        SorteoTransparencyFileStore.ResolveDirectory(contentRoot, webRoot,
            Path.Combine("privado", "transparencia")));
    var refusedPublicStorage = false;
    try
    {
        SorteoTransparencyFileStore.ResolveDirectory(contentRoot, webRoot,
            Path.Combine("wwwroot", "transparencia"));
    }
    catch (InvalidOperationException) { refusedPublicStorage = true; }
    True(refusedPublicStorage, "La API no debe guardar un documento privado en wwwroot.");
    return Task.CompletedTask;
}

static Task TestTransparencyNumbersAsync()
{
    var unsold = SorteoTransparencyNumbers.GetUnsold(100, ["043", "003", "042"]);
    Equal(97, unsold.Length);
    Equal("000", unsold[0]);
    Equal("099", unsold[^1]);
    True(!unsold.Contains("003") && !unsold.Contains("042") && !unsold.Contains("043"),
        "Los números vendidos no deben aparecer en el documento.");
    True(unsold.SequenceEqual(unsold.OrderBy(number => number, StringComparer.Ordinal)),
        "Los números no vendidos deben estar ordenados.");

    var special = SorteoTransparencyNumbers.GetUnsold(60000, ["00001", "60000"]);
    Equal(59998, special.Length);
    Equal("00002", special[0]);
    Equal("59999", special[^1]);
    Equal(0, SorteoTransparencyNumbers.GetUnsold(3, ["0", "1", "2"]).Length);

    Reject(0, []);
    Reject(3, ["0", "0"]);
    Reject(3, ["3"]);
    Reject(100, ["42"]);
    Reject(100, ["abc"]);
    Reject(60000, ["00000"]);

    return Task.CompletedTask;

    static void Reject(int total, string[] sold)
    {
        var rejected = false;
        try { SorteoTransparencyNumbers.GetUnsold(total, sold); }
        catch (InvalidOperationException) { rejected = true; }
        True(rejected, "Una venta inconsistente debe bloquear la generación del documento.");
    }
}

static Task TestTransparencyLargePdfAsync()
{
    var numbers = Enumerable.Range(42001, 18000)
        .Select(number => number.ToString("D5"))
        .ToArray();
    var now = ApplicationClock.NowOffset;
    var pdf = SorteoTransparencyPdf.Generate(1, "Sorteo de prueba", "Sorteos Global Broker", now.DateTime.AddDays(1),
        "CDMX", now.AddMinutes(-15).DateTime, now.DateTime, 60000, 42000,
        numbers);
    True(pdf.AsSpan().StartsWith("%PDF-"u8), "El documento grande debe generarse como PDF.");
    True(pdf.Length > 100000, "El PDF debe contener los miles de números no vendidos.");
    Equal("Relación de boletos no vendidos Sorteos Global Broker",
        SorteoTransparencyPdf.FooterText("Sorteos Global Broker"));
    return Task.CompletedTask;
}

static Task TestTransparencyPdfLotteryTimeAsync()
{
    var sorteo = new Sorteos { ZonaHoraria = "CDMX" };
    ISorteoTimeService timeService = new SorteoTimeService();
    var closedAt = new DateTimeOffset(2026, 9, 29, 16, 50, 0, TimeSpan.FromHours(-6));
    var issuedAt = new DateTimeOffset(2026, 9, 29, 17, 38, 6, TimeSpan.FromHours(-7));

    Equal("29/09/2026 16:50:00", SorteoTransparencyPdf.Format(
        timeService.ConvertToSorteoTime(closedAt, sorteo).DateTime));
    Equal("29/09/2026 18:38:06", SorteoTransparencyPdf.Format(
        timeService.ConvertToSorteoTime(issuedAt, sorteo).DateTime));
    return Task.CompletedTask;
}

static Task TestLotteryAvailabilityPolicyAsync()
{
    var fecha = new DateTime(2026, 9, 2, 20, 0, 0, DateTimeKind.Unspecified);
    var sorteo = new Sorteos { Fecha = fecha };
    ISorteoTimeService timeService = new SorteoTimeService();
    var drawAt = timeService.ResolveScheduledTime(sorteo);

    True(
        SorteoDisponibilidadPolicy.VentaDisponible(sorteo, drawAt.AddMinutes(-90).AddSeconds(-1), timeService),
        "La venta debe permanecer disponible antes del límite de 90 minutos.");
    Equal(
        SorteoEstados.Disponible,
        SorteoDisponibilidadPolicy.ObtenerEstado(sorteo, drawAt.AddMinutes(-90).AddSeconds(-1), timeService));

    True(
        !SorteoDisponibilidadPolicy.VentaDisponible(sorteo, drawAt.AddMinutes(-90), timeService),
        "La venta debe bloquearse exactamente 90 minutos antes.");
    Equal(
        SorteoEstados.ProximoAIniciar,
        SorteoDisponibilidadPolicy.ObtenerEstado(sorteo, drawAt.AddMinutes(-90), timeService));
    Equal(
        SorteoEstados.EnProceso,
        SorteoDisponibilidadPolicy.ObtenerEstado(sorteo, drawAt, timeService));

    sorteo.Fecha = new DateTime(2026, 9, 29, 20, 0, 0);
    Equal(new DateTimeOffset(2026, 9, 29, 17, 30, 0, TimeSpan.FromHours(-7)),
        SorteoDisponibilidadPolicy.ObtenerInicioBloqueo(sorteo, timeService));
    Equal(new DateTime(2026, 9, 29, 18, 30, 0),
        timeService.ConvertToSorteoTime(
            SorteoDisponibilidadPolicy.ObtenerInicioBloqueo(sorteo, timeService), sorteo).DateTime);
    True(SorteoDisponibilidadPolicy.VentaDisponible(sorteo,
        new DateTimeOffset(2026, 9, 29, 17, 29, 59, TimeSpan.FromHours(-7)), timeService),
        "A las 17:29:59 de Hermosillo todavía debe permitirse la venta.");
    True(!SorteoDisponibilidadPolicy.VentaDisponible(sorteo,
        new DateTimeOffset(2026, 9, 29, 17, 30, 0, TimeSpan.FromHours(-7)), timeService),
        "El cierre debe aplicarse al instante correcto, aun con otra zona en el servidor.");

    sorteo.NumeroGanador = "000123";
    Equal(
        SorteoEstados.Finalizado,
        SorteoDisponibilidadPolicy.ObtenerEstado(sorteo, drawAt.AddHours(1), timeService));

    return Task.CompletedTask;
}

static async Task TestCurrentLotteryAcrossTimeZonesAsync()
{
    var options = CreateOptions($"eLottoLotteryTimeZones_{Guid.NewGuid():N}");
    try
    {
        var seeded = await RecreateAndSeedAsync(options, prizeQuantity: 1);
        int sonoraId;
        await using (var context = new eLottoContext(options))
        {
            var mexicoCity = await context.Sorteos.SingleAsync(x => x.Id == seeded.SorteoId);
            mexicoCity.Fecha = new DateTime(2026, 9, 29, 11, 10, 0);
            mexicoCity.ZonaHoraria = "CDMX";
            var sonora = new Sorteos
            {
                Nombre = "Sorteo Sonora",
                Imagen1 = "sonora.webp",
                Fecha = new DateTime(2026, 9, 29, 10, 30, 0),
                ZonaHoraria = "Hermosillo",
                PrecioBoleto = 1,
                PrecioPorMil = 1,
                CantidadBoletos = 100
            };
            context.Sorteos.Add(sonora);
            await context.SaveChangesAsync();
            sonoraId = sonora.Id;
        }

        await using var verification = new eLottoContext(options);
        var repository = new UserLotteryRepository(verification);
        var serverNow = new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.FromHours(-7));
        Equal(seeded.SorteoId, (await repository.GetCurrentAsync(serverNow, CancellationToken.None))!.Id);
        Equal(seeded.SorteoId,
            (await repository.GetCurrentAsync(serverNow.AddMinutes(20), CancellationToken.None))!.Id);
        Equal(sonoraId,
            (await repository.GetCurrentAsync(serverNow.AddMinutes(35), CancellationToken.None))!.Id);
    }
    finally { await DeleteDatabaseAsync(options); }
}

static async Task TestSalesGuardAcrossTimeZonesAsync()
{
    var options = CreateOptions($"eLottoSalesTimeZones_{Guid.NewGuid():N}");
    try
    {
        var seeded = await RecreateAndSeedAsync(options, prizeQuantity: 1);
        await using var context = new eLottoContext(options);
        var sorteo = await context.Sorteos.SingleAsync(x => x.Id == seeded.SorteoId);
        var repository = new UserLotteryRepository(context);
        var timeService = new SorteoTimeService();
        await repository.EnsureTicketsAsync(sorteo, CancellationToken.None);

        sorteo.Fecha = timeService.ConvertToSorteoTime(
            ApplicationClock.NowOffset.AddMinutes(89), sorteo).DateTime;
        await context.SaveChangesAsync();
        var closed = await repository.PreReserveAsync(sorteo.Id, seeded.UserId,
            "CERR0001", ["000"], CancellationToken.None);
        True(!closed.Ok, "Un sorteo que inicia en 89 minutos debe estar cerrado desde T-90.");

        sorteo.Fecha = timeService.ConvertToSorteoTime(
            ApplicationClock.NowOffset.AddMinutes(100), sorteo).DateTime;
        await context.SaveChangesAsync();
        var open = await repository.PreReserveAsync(sorteo.Id, seeded.UserId,
            "ABIE0001", ["000"], CancellationToken.None);
        True(open.Ok, "Un sorteo que inicia en 100 minutos debe permitir el preapartado.");

        var service = new UserLotteryService(repository, null,
            new NullConfirmedTicketDeliveryService(), null,
            NullLogger<UserLotteryService>.Instance,
            Options.Create(new LotteryRulesOptions()), timeService);
        var current = await service.GetCurrentAsync(CancellationToken.None);
        True(current is not null && current.VentaDisponible,
            "La pantalla debe mostrar el mismo sorteo como disponible.");
        True(current.SegundosParaInicio is > 5900 and <= 6000,
            "El contador de inicio debe usar la hora real de la zona del sorteo.");
        True(current.SegundosParaCierreVentas is > 500 and <= 600,
            "El contador de cierre debe quedar a 10 minutos de T-90.");
    }
    finally { await DeleteDatabaseAsync(options); }
}

static async Task TestSevenMinuteReservationAsync()
{
    var options = CreateOptions($"eLottoSevenMinuteReservation_{Guid.NewGuid():N}");
    try
    {
        var seeded = await RecreateAndSeedAsync(options, prizeQuantity: 1, initialBalance: 10m);
        await using var context = new eLottoContext(options);
        var sorteo = await context.Sorteos.SingleAsync(x => x.Id == seeded.SorteoId);
        var repository = new UserLotteryRepository(context);
        await repository.EnsureTicketsAsync(sorteo, CancellationToken.None);

        var ticket = await context.SorteosBoletos.SingleAsync(x =>
            x.SorteosId == sorteo.Id && x.Numero == "000");
        ticket.PreApartado = true;
        ticket.UsuarioId = seeded.UserId;
        ticket.FolioCompra = "ABCD2345";
        ticket.Fecha = ApplicationClock.Now.AddMinutes(-7).AddSeconds(-30);
        await context.SaveChangesAsync();

        var expired = await repository.ConfirmPurchaseAsync(sorteo.Id, seeded.UserId,
            ticket.FolioCompra, ["000"], CancellationToken.None);
        Equal("tickets_unavailable", expired.Code);
        True(!expired.Ok, "Un preapartado de más de siete minutos no puede comprarse.");

        ticket.Fecha = ApplicationClock.Now.AddMinutes(-6).AddSeconds(-30);
        await context.SaveChangesAsync();
        var valid = await repository.ConfirmPurchaseAsync(sorteo.Id, seeded.UserId,
            ticket.FolioCompra, ["000"], CancellationToken.None);
        True(valid.Ok, "Un preapartado de menos de siete minutos debe poder comprarse.");

        var manual = await repository.PreReserveAsync(sorteo.Id, seeded.UserId,
            "ABCD2346", ["001"], CancellationToken.None);
        True(manual.Ok, "El preapartado manual debe concretarse.");
        var manualTicket = await context.SorteosBoletos.AsNoTracking().SingleAsync(x =>
            x.SorteosId == sorteo.Id && x.Numero == "001");
        Equal(TimeSpan.FromMinutes(7), manual.ExpiresAt!.Value.DateTime - manualTicket.Fecha);

        var random = await repository.PreReserveRandomAsync(sorteo.Id, seeded.UserId,
            1, "inicio", "002", CancellationToken.None);
        True(random.Ok, "El preapartado aleatorio debe concretarse.");
        var randomTicket = await context.SorteosBoletos.AsNoTracking().SingleAsync(x =>
            x.SorteosId == sorteo.Id && x.Numero == "002");
        Equal(TimeSpan.FromMinutes(7), random.ExpiresAt!.Value.DateTime - randomTicket.Fecha);

        var service = new UserLotteryService(repository, null,
            new NullConfirmedTicketDeliveryService(), null,
            NullLogger<UserLotteryService>.Instance,
            Options.Create(new LotteryRulesOptions()), new SorteoTimeService());
        var response = await service.PreReserveManualAsync(seeded.UserId,
            new ManualPreReserveDto { FolioCompra = "ABCD2347", Numeros = ["003"] },
            CancellationToken.None);
        True(response.Ok && response.SegundosParaExpirar is > 0 and <= 420,
            "El contador entregado al frontend no debe superar siete minutos.");
    }
    finally { await DeleteDatabaseAsync(options); }
}

static async Task TestPurchaseUsesLotteryTimeZoneAsync()
{
    var options = CreateOptions($"eLottoPurchaseTimeZone_{Guid.NewGuid():N}");
    try
    {
        var seeded = await RecreateAndSeedAsync(options, prizeQuantity: 1, initialBalance: 10m);
        await using var context = new eLottoContext(options);
        var sorteo = await context.Sorteos.SingleAsync(x => x.Id == seeded.SorteoId);
        var repository = new UserLotteryRepository(context);
        ISorteoTimeService timeService = new SorteoTimeService();
        await repository.EnsureTicketsAsync(sorteo, CancellationToken.None);

        var ticket = await context.SorteosBoletos.SingleAsync(x =>
            x.SorteosId == sorteo.Id && x.Numero == "000");
        ticket.PreApartado = true;
        ticket.UsuarioId = seeded.UserId;
        ticket.FolioCompra = "ABCD3456";
        ticket.Fecha = ApplicationClock.Now;
        sorteo.Fecha = timeService.ConvertToSorteoTime(
            ApplicationClock.NowOffset.AddMinutes(89), sorteo).DateTime;
        await context.SaveChangesAsync();

        await ThrowsAsync<InvalidOperationException>(() => repository.ConfirmPurchaseAsync(
            sorteo.Id, seeded.UserId, ticket.FolioCompra, ["000"], CancellationToken.None));
        Equal(0, await context.BoletosConfirmados.CountAsync(x => x.SorteosId == sorteo.Id));
        Equal(10m, (await context.UserWallets.AsNoTracking().SingleAsync(x => x.Id == seeded.WalletId)).Balance);

        sorteo.Fecha = timeService.ConvertToSorteoTime(
            ApplicationClock.NowOffset.AddMinutes(100), sorteo).DateTime;
        await context.SaveChangesAsync();
        var serverBefore = ApplicationClock.NowOffset;
        var result = await repository.ConfirmPurchaseAsync(
            sorteo.Id, seeded.UserId, ticket.FolioCompra, ["000"], CancellationToken.None);
        var serverAfter = ApplicationClock.NowOffset;
        True(result.Ok, "La compra debe aceptarse cuando restan 100 minutos y la reserva sigue vigente.");

        var confirmed = await context.BoletosConfirmados.AsNoTracking().SingleAsync(x =>
            x.SorteosId == sorteo.Id && x.Numero == "000");
        var movement = await context.WalletTransactions.AsNoTracking().SingleAsync(x =>
            x.SorteoId == sorteo.Id && x.Type == WalletTransactionType.Purchase);
        var wallet = await context.UserWallets.AsNoTracking().SingleAsync(x => x.Id == seeded.WalletId);
        var zoneBefore = timeService.ConvertToSorteoTime(serverBefore, sorteo).DateTime;
        var zoneAfter = timeService.ConvertToSorteoTime(serverAfter, sorteo).DateTime;
        True(confirmed.Fecha >= zoneBefore && confirmed.Fecha <= zoneAfter,
            "La fecha del boleto debe corresponder al instante de compra en la zona del sorteo.");
        Equal(confirmed.Fecha, movement.CreatedAt);
        Equal(confirmed.Fecha, movement.CompletedAt!.Value);
        True(wallet.UpdatedAt >= serverBefore.DateTime && wallet.UpdatedAt <= serverAfter.DateTime,
            "La cartera compartida debe conservar la hora local del servidor.");
    }
    finally { await DeleteDatabaseAsync(options); }
}

static async Task TestStripeDepositTimeZoneAsync()
{
    var options = CreateOptions($"eLottoDepositTimeZone_{Guid.NewGuid():N}");
    try
    {
        var seeded = await RecreateAndSeedAsync(options, prizeQuantity: 1);
        ISorteoTimeService timeService = new SorteoTimeService();
        DateTime createdAt;
        await using (var setup = new eLottoContext(options))
        {
            var sorteo = await setup.Sorteos.SingleAsync(x => x.Id == seeded.SorteoId);
            sorteo.ZonaHoraria = "Cancun";
            createdAt = timeService.ConvertToSorteoTime(
                ApplicationClock.NowOffset.AddMinutes(-30), sorteo).DateTime;
            var deposit = await setup.WalletTransactions.SingleAsync(x => x.Id == seeded.TransactionId);
            deposit.CreatedAt = createdAt;
            await setup.SaveChangesAsync();

            var repository = new SorteosRepository(setup);
            var updated = await repository.GetByIdAsync(seeded.SorteoId);
            updated.ZonaHoraria = "CDMX";
            await ThrowsAsync<InvalidOperationException>(() => repository.UpdateAsync(updated));
        }

        var serverBefore = ApplicationClock.NowOffset;
        await ProcessEventAsync(options, CreateSucceededEvent(
            "evt_deposit_timezone", seeded.PaymentIntentId,
            seeded.TransactionId, seeded.UserId, 300m));
        var serverAfter = ApplicationClock.NowOffset;

        await using var verification = new eLottoContext(options);
        var transaction = await verification.WalletTransactions.AsNoTracking()
            .SingleAsync(x => x.Id == seeded.TransactionId);
        var wallet = await verification.UserWallets.AsNoTracking()
            .SingleAsync(x => x.Id == seeded.WalletId);
        var sorteoActual = await verification.Sorteos.AsNoTracking()
            .SingleAsync(x => x.Id == seeded.SorteoId);
        Equal(createdAt, transaction.CreatedAt);
        Equal(WalletTransactionStatus.Completed, transaction.Status);
        var zoneBefore = timeService.ConvertToSorteoTime(serverBefore, sorteoActual).DateTime;
        var zoneAfter = timeService.ConvertToSorteoTime(serverAfter, sorteoActual).DateTime;
        True(transaction.CompletedAt.HasValue &&
            transaction.CompletedAt.Value >= zoneBefore && transaction.CompletedAt.Value <= zoneAfter,
            "La confirmación tardía de Stripe debe usar la zona del sorteo de la transacción.");
        True(wallet.UpdatedAt >= serverBefore.DateTime && wallet.UpdatedAt <= serverAfter.DateTime,
            "La cartera compartida debe mantener la hora local del servidor.");
        var scratchcards = await verification.SorteosRascaditos.AsNoTracking()
            .Where(x => x.WalletTransactionOrigenId == seeded.TransactionId)
            .ToListAsync();
        True(scratchcards.Count > 0 && scratchcards.All(x =>
            x.FechaGeneracion >= zoneBefore && x.FechaGeneracion <= zoneAfter),
            "Los rascaditos generados por el depósito deben fecharse en la zona del sorteo.");

        var cancelledDeposit = await AddPendingDepositAsync(options,
            seeded.UserId, seeded.WalletId, seeded.SorteoId, 300m);
        var cancelledEvent = CreateSucceededEvent("evt_deposit_timezone_cancelled",
            cancelledDeposit.PaymentIntentId, cancelledDeposit.TransactionId, seeded.UserId, 300m);
        cancelledEvent.Type = "payment_intent.canceled";
        var beforeCancellation = ApplicationClock.NowOffset;
        await ProcessEventAsync(options, cancelledEvent);
        var afterCancellation = ApplicationClock.NowOffset;
        var cancelled = await verification.WalletTransactions.AsNoTracking()
            .SingleAsync(x => x.Id == cancelledDeposit.TransactionId);
        Equal(WalletTransactionStatus.Cancelled, cancelled.Status);
        True(cancelled.CompletedAt.HasValue &&
            cancelled.CompletedAt.Value >= timeService.ConvertToSorteoTime(beforeCancellation, sorteoActual).DateTime &&
            cancelled.CompletedAt.Value <= timeService.ConvertToSorteoTime(afterCancellation, sorteoActual).DateTime,
            "Stripe debe fechar también la cancelación en la zona del sorteo.");
    }
    finally { await DeleteDatabaseAsync(options); }
}

static async Task TestStripeApplicationMetadataAsync()
{
    var options = CreateOptions($"eLottoApplicationMetadata_{Guid.NewGuid():N}");
    try
    {
        var seeded = await RecreateAndSeedAsync(options, prizeQuantity: 1);
        var wrongApplication = CreateSucceededEvent("evt_wrong_application", seeded.PaymentIntentId,
            seeded.TransactionId, seeded.UserId, 300m);
        var intent = (PaymentIntent)wrongApplication.Data.Object;
        intent.Metadata["Application"] = "eLotto";
        await ProcessEventAsync(options, wrongApplication);

        await using (var rejected = new eLottoContext(options))
        {
            Equal(WalletTransactionStatus.Pending,
                (await rejected.WalletTransactions.AsNoTracking()
                    .SingleAsync(x => x.Id == seeded.TransactionId)).Status);
        }

        await ProcessEventAsync(options, CreateSucceededEvent("evt_configured_application",
            seeded.PaymentIntentId, seeded.TransactionId, seeded.UserId, 300m));
        await using var accepted = new eLottoContext(options);
        Equal(WalletTransactionStatus.Completed,
            (await accepted.WalletTransactions.AsNoTracking()
                .SingleAsync(x => x.Id == seeded.TransactionId)).Status);
    }
    finally { await DeleteDatabaseAsync(options); }
}

static async Task TestLotteryRescheduleWithoutWinnerAsync()
{
    var databaseName = $"eLottoLotteryResultTests_{Guid.NewGuid():N}";
    var options = CreateOptions(databaseName);
    try
    {
        var seeded = await RecreateAndSeedAsync(options, prizeQuantity: 1);
        DateTime originalDate;
        DateTime futureOriginalDate;
        int futureLotteryId;
        ISorteoTimeService timeService = new SorteoTimeService();

        await using (var setup = new eLottoContext(options))
        {
            var current = await setup.Sorteos.SingleAsync(x => x.Id == seeded.SorteoId);
            current.ZonaHoraria = "Cancun";
            var originalInstant = ApplicationClock.NowOffset.AddHours(-1);
            originalDate = timeService.ConvertToSorteoTime(originalInstant, current).DateTime;
            current.Fecha = originalDate;

            var future = new Sorteos
            {
                Nombre = "Sorteo futuro",
                Imagen1 = "future.webp",
                Fecha = timeService.ConvertToSorteoTime(
                    originalInstant.AddHours(1), new Sorteos { ZonaHoraria = "Tijuana" }).DateTime,
                ZonaHoraria = "Tijuana",
                PrecioBoleto = 1,
                PrecioPorMil = 1,
                CantidadBoletos = 100
            };
            setup.Sorteos.Add(future);
            setup.SorteosBoletos.Add(new SorteosBoletos
            {
                SorteosId = seeded.SorteoId,
                Numero = "043",
                FolioCompra = "RESERVA1",
                UsuarioId = seeded.UserId,
                Fecha = ApplicationClock.Now,
                PreApartado = true,
                Apartado = true,
                Aviso = true
            });
            setup.BoletosConfirmados.Add(new BoletosConfirmados
            {
                SorteosId = seeded.SorteoId,
                Numero = "042",
                FolioCompra = "VENDIDO1",
                UsuarioId = seeded.UserId,
                UsuarioIdConfirm = seeded.UserId,
                WhatsAppConfirm = "5216620000000",
                CuentaAsignada = "cartera",
                Fecha = ApplicationClock.Now
            });
            await setup.SaveChangesAsync();
            futureLotteryId = future.Id;
            futureOriginalDate = future.Fecha;
            True(futureOriginalDate < originalDate,
                "El sorteo posterior debe tener una hora de pared anterior para probar el orden entre zonas.");
            await AddPublishedTransparencyFixtureAsync(setup, seeded.SorteoId);
        }

        await using (var verificationContext = new eLottoContext(options))
        {
            var repository = new SorteosRepository(verificationContext);
            var winner = await repository.VerifyWinningNumberAsync(
                seeded.SorteoId,
                "42",
                CancellationToken.None);
            True(winner.HayGanador, "El número vendido debe identificar al ganador.");
            Equal("042", winner.NumeroGanador);
            Equal("scratchtest", winner.UsuarioGanador);
            Equal("+5266*****000", winner.WhatsAppGanador);

            var noWinner = await repository.VerifyWinningNumberAsync(
                seeded.SorteoId,
                "43",
                CancellationToken.None);
            True(!noWinner.HayGanador, "El número disponible no debe identificar ganador.");

            var result = await repository.RescheduleWithoutWinnerAsync(
                seeded.SorteoId,
                "43",
                CancellationToken.None);
            Equal(SorteoResultadoStatus.Ok, result.Status);
            Equal(2, result.SorteosReprogramados);
            Equal(originalDate.AddDays(7), result.NuevaFecha!.Value);
        }

        await using var assertionContext = new eLottoContext(options);
        Equal(
            originalDate.AddDays(7),
            await assertionContext.Sorteos
                .Where(x => x.Id == seeded.SorteoId)
                .Select(x => x.Fecha)
                .SingleAsync());
        Equal(
            futureOriginalDate.AddDays(7),
            await assertionContext.Sorteos
                .Where(x => x.Id == futureLotteryId)
                .Select(x => x.Fecha)
                .SingleAsync());
        Equal(
            1,
            await assertionContext.BoletosConfirmados.CountAsync(
                x => x.SorteosId == seeded.SorteoId && x.Numero == "042"));
        var released = await assertionContext.SorteosBoletos
            .SingleAsync(x => x.SorteosId == seeded.SorteoId && x.Numero == "043");
        True(
            !released.PreApartado && !released.Apartado && !released.Aviso &&
            released.UsuarioId == 0 && released.FolioCompra == string.Empty,
            "El boleto no vendido debe regresar a disponibilidad.");
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}

static async Task TestLotteryMinimumSaleRescheduleAsync()
{
    var options = CreateOptions($"eLottoLotteryMinimumSaleTests_{Guid.NewGuid():N}");
    try
    {
        var seeded = await RecreateAndSeedAsync(options, prizeQuantity: 1);
        DateTime originalDate;
        await using (var setup = new eLottoContext(options))
        {
            var lottery = await setup.Sorteos.SingleAsync(x => x.Id == seeded.SorteoId);
            originalDate = ApplicationClock.Now.AddHours(-1);
            lottery.Fecha = originalDate;
            lottery.PorcentajeMinimoVenta = 85;
            setup.BoletosConfirmados.Add(new BoletosConfirmados
            {
                SorteosId = seeded.SorteoId,
                Numero = "042",
                FolioCompra = "VENDIDO1",
                UsuarioId = seeded.UserId,
                UsuarioIdConfirm = seeded.UserId,
                WhatsAppConfirm = "5216620000000",
                CuentaAsignada = "cartera",
                Fecha = ApplicationClock.Now
            });
            await setup.SaveChangesAsync();
        }

        await using (var context = new eLottoContext(options))
        {
            var repository = new SorteosRepository(context);

            var finalization = await repository.FinalizeWinnerAsync(
                seeded.SorteoId,
                "42",
                CancellationToken.None);
            Equal(SorteoResultadoStatus.VentaMinimaNoAlcanzada, finalization.Status);

            var reschedule = await repository.RescheduleWithoutWinnerAsync(
                seeded.SorteoId,
                null,
                CancellationToken.None);
            Equal(SorteoResultadoStatus.Ok, reschedule.Status);
            True(reschedule.NumeroGanador == null, "La reprogramación por venta mínima no debe capturar número ganador.");
            Equal(originalDate.AddDays(7), reschedule.NuevaFecha!.Value);
        }

        await using var assertionContext = new eLottoContext(options);
        Equal(1, await assertionContext.BoletosConfirmados.CountAsync(x => x.SorteosId == seeded.SorteoId));
        Equal(0, await assertionContext.GanadoresSorteos.CountAsync(x => x.SorteoId == seeded.SorteoId));
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}

static async Task TestLotteryFinalizationAsync()
{
    var databaseName = $"eLottoLotteryFinalizationTests_{Guid.NewGuid():N}";
    var options = CreateOptions(databaseName);
    try
    {
        var seeded = await RecreateAndSeedAsync(options, prizeQuantity: 3);
        ISorteoTimeService timeService = new SorteoTimeService();
        await using (var setupZone = new eLottoContext(options))
        {
            var lottery = await setupZone.Sorteos.SingleAsync(x => x.Id == seeded.SorteoId);
            lottery.ZonaHoraria = "Cancun";
            await setupZone.SaveChangesAsync();
        }
        var revealedWinnerId = await AddScratchcardAsync(options, seeded, isWinner: true, prizeAmount: 50m);
        await RevealWithNewContextAsync(options, revealedWinnerId, seeded.UserId);
        await AddScratchcardAsync(options, seeded, isWinner: true, prizeAmount: 50m);
        await AddScratchcardAsync(options, seeded, isWinner: false);

        await using (var setup = new eLottoContext(options))
        {
            var sorteo = await setup.Sorteos.SingleAsync(x => x.Id == seeded.SorteoId);
            sorteo.Fecha = timeService.ConvertToSorteoTime(
                ApplicationClock.NowOffset.AddHours(-1), sorteo).DateTime;
            setup.BoletosConfirmados.AddRange(
                new BoletosConfirmados
                {
                    SorteosId = seeded.SorteoId,
                    Numero = "042",
                    FolioCompra = "COMPRA01",
                    UsuarioId = seeded.UserId,
                    Fecha = ApplicationClock.Now.AddMinutes(-20),
                    UsuarioIdConfirm = seeded.UserId,
                    WhatsAppConfirm = "+5216620000000",
                    CuentaAsignada = string.Empty
                },
                new BoletosConfirmados
                {
                    SorteosId = seeded.SorteoId,
                    Numero = "077",
                    FolioCompra = "COMPRA01",
                    UsuarioId = seeded.UserId,
                    Fecha = ApplicationClock.Now.AddMinutes(-20),
                    UsuarioIdConfirm = seeded.UserId,
                    WhatsAppConfirm = "+5216620000000",
                    CuentaAsignada = string.Empty
                });
            setup.SorteosBoletos.Add(new SorteosBoletos
            {
                SorteosId = seeded.SorteoId,
                Numero = "099",
                FolioCompra = string.Empty,
                Fecha = ApplicationClock.Now
            });
            await setup.SaveChangesAsync();
            var blocked = await new SorteosRepository(setup).FinalizeWinnerAsync(
                seeded.SorteoId, "42", CancellationToken.None);
            Equal(SorteoResultadoStatus.DocumentoTransparenciaNoPublicado, blocked.Status);
            await AddPublishedTransparencyFixtureAsync(setup, seeded.SorteoId);
        }

        var finalizedBefore = ApplicationClock.NowOffset;
        await using (var finalizationContext = new eLottoContext(options))
        {
            var repository = new SorteosRepository(finalizationContext);
            var result = await repository.FinalizeWinnerAsync(
                seeded.SorteoId,
                "42",
                CancellationToken.None);
            Equal(SorteoResultadoStatus.Ok, result.Status);
            Equal(1, result.ComprasArchivadas);
            Equal(1, result.RascaditosGanadoresArchivados);
            Equal(2, result.RascaditosCaducados);
            True(result.Ganador.Referencia == null,
                "Un ganador sin referidor no debe devolver un bloque de referido.");
        }
        var finalizedAfter = ApplicationClock.NowOffset;

        await using (var assertionContext = new eLottoContext(options))
        {
            Equal(0, await assertionContext.SorteosBoletos.CountAsync());
            Equal(0, await assertionContext.BoletosConfirmados.CountAsync());
            Equal(0, await assertionContext.SorteosRascaditos.CountAsync());

            var purchase = await assertionContext.BoletosConfirmadosHistorial.SingleAsync();
            Equal(2, purchase.CantidadBoletos);
            Equal("[\"042\",\"077\"]", purchase.NumerosJson);
            Equal(2m, purchase.ImporteTotal);

            var archivedScratchcard = await assertionContext
                .SorteosRascaditosGanadoresHistorial
                .SingleAsync();
            Equal(seeded.UserId, archivedScratchcard.UsuarioId);
            True(
                !string.IsNullOrWhiteSpace(archivedScratchcard.MatrizResultado) &&
                !string.IsNullOrWhiteSpace(archivedScratchcard.LineaGanadora),
                "El histórico debe conservar la representación visual ganadora.");

            var lottery = await assertionContext.Sorteos.SingleAsync(x => x.Id == seeded.SorteoId);
            Equal("042", lottery.NumeroGanador);
            Equal(seeded.UserId, lottery.UsuarioIdGanador);
            var finalizedAt = await assertionContext.GanadoresSorteos
                .Where(x => x.SorteoId == seeded.SorteoId)
                .Select(x => x.FechaFin)
                .SingleAsync();
            True(finalizedAt >= timeService.ConvertToSorteoTime(finalizedBefore, lottery).DateTime &&
                finalizedAt <= timeService.ConvertToSorteoTime(finalizedAfter, lottery).DateTime,
                "La finalización debe guardarse con la hora del sorteo.");
            Equal(finalizedAt, purchase.FechaArchivado);
            Equal(finalizedAt, archivedScratchcard.FechaArchivado);
            Equal(0, await assertionContext.ReferralWinnerCashRewards.CountAsync());
        }

        await using var historyContext = new eLottoContext(options);
        var finalizedWinner = await new SorteosRepository(historyContext)
            .GetLatestFinalizedWinnerAsync(CancellationToken.None);
        True(finalizedWinner.Referencia == null,
            "La consulta persistida no debe mostrar referido para un ganador normal.");
        var history = await new UserPrizeHistoryService(historyContext, new SorteoTimeService())
            .GetAsync(seeded.UserId, CancellationToken.None);
        Equal(2, history.TotalPremios);
        Equal(1, history.TotalSorteos);
        Equal(1, history.TotalRascaditos);
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}

static async Task TestWinnerHistoryAcrossTimeZonesAsync()
{
    var options = CreateOptions($"eLottoWinnerHistoryZones_{Guid.NewGuid():N}");
    try
    {
        var seeded = await RecreateAndSeedAsync(options, prizeQuantity: 1);
        ISorteoTimeService timeService = new SorteoTimeService();
        var olderMoment = new DateTimeOffset(2026, 9, 29, 16, 0, 0, TimeSpan.Zero);
        var newerMoment = olderMoment.AddMinutes(30);
        int laterLotteryId;
        await using (var setup = new eLottoContext(options))
        {
            var first = await setup.Sorteos.SingleAsync(x => x.Id == seeded.SorteoId);
            first.ZonaHoraria = "Cancun";
            first.Fecha = timeService.ConvertToSorteoTime(olderMoment, first).DateTime;
            first.NumeroGanador = "042";
            first.UsuarioIdGanador = seeded.UserId;
            first.NombreGanador = "scratchtest";
            var second = new Sorteos
            {
                Nombre = "Sorteo posterior",
                Imagen1 = "later.webp",
                ZonaHoraria = "Tijuana",
                Fecha = timeService.ConvertToSorteoTime(
                    newerMoment, new Sorteos { ZonaHoraria = "Tijuana" }).DateTime,
                NumeroGanador = "043",
                UsuarioIdGanador = seeded.UserId,
                NombreGanador = "scratchtest",
                PrecioBoleto = 1m,
                PrecioPorMil = 1m,
                CantidadBoletos = 100
            };
            setup.Sorteos.Add(second);
            await setup.SaveChangesAsync();
            laterLotteryId = second.Id;
            True(second.Fecha < first.Fecha,
                "El sorteo posterior debe tener una hora visible menor.");
            setup.GanadoresSorteos.AddRange(
                new GanadoresSorteos
                {
                    SorteoId = first.Id, NumeroGanador = "042", FolioSorteo = "FIRST",
                    Nombre = first.Nombre, FechaFin = timeService.ConvertToSorteoTime(olderMoment, first).DateTime,
                    WhatsAppGanador = "5216620000000", UsuarioIdGanador = seeded.UserId,
                    NombreGanador = "scratchtest"
                },
                new GanadoresSorteos
                {
                    SorteoId = second.Id, NumeroGanador = "043", FolioSorteo = "SECOND",
                    Nombre = second.Nombre, FechaFin = timeService.ConvertToSorteoTime(newerMoment, second).DateTime,
                    WhatsAppGanador = "5216620000000", UsuarioIdGanador = seeded.UserId,
                    NombreGanador = "scratchtest"
                });
            await setup.SaveChangesAsync();
        }

        await using var verification = new eLottoContext(options);
        var latest = await new SorteosRepository(verification)
            .GetLatestFinalizedWinnerAsync(CancellationToken.None);
        Equal(laterLotteryId, latest.SorteoId);
        var history = await new UserPrizeHistoryService(verification, timeService)
            .GetAsync(seeded.UserId, CancellationToken.None);
        Equal(2, history.TotalSorteos);
        Equal(laterLotteryId, history.Premios[0].SorteoId);
    }
    finally { await DeleteDatabaseAsync(options); }
}

static Task TestDepositCalculationAsync()
{
    var examples = new Dictionary<decimal, long>
    {
        [0m] = 0,
        [0.01m] = 0,
        [29m] = 0,
        [29.99m] = 0,
        [30m] = 1,
        [30.01m] = 1,
        [59.99m] = 1,
        [60m] = 2,
        [89.99m] = 2,
        [90m] = 3,
        [100m] = 3,
        [150m] = 5,
        [299m] = 9,
        [299.99m] = 9,
        [300m] = 10,
        [419.99m] = 13,
        [420m] = 14,
        [2000m] = 66
    };

    foreach (var example in examples)
        Equal(
            example.Value,
            ScratchcardDistributionPolicy.CalculateScratchcards(example.Key, 30m),
            $"aepósito {example.Key}");

    Equal(0L, ScratchcardDistributionPolicy.CalculateScratchcards(300m, 30m, false));
    Equal(4L, ScratchcardDistributionPolicy.CalculateScratchcards(100m, 25m));
    Equal(4L, ScratchcardDistributionPolicy.CalculateScratchcards(300m, 75m));
    Equal(0L, ScratchcardDistributionPolicy.CalculateScratchcards(300m, 0m));
    return Task.CompletedTask;
}

static Task TestCapacityAsync()
{
    Equal(2000L, ScratchcardDistributionPolicy.CalculateCapacity(200, 1, 10));
    Equal(10L, ScratchcardDistributionPolicy.CalculateCapacity(3, 2, 7));
    Equal(10L, ScratchcardDistributionPolicy.CalculateStrategicRemainingCapacity(20, 10, 3, 1, 10));
    Equal(3L, ScratchcardDistributionPolicy.CalculateStrategicRemainingCapacity(20, 17, 3, 1, 10));
    Equal(30L, ScratchcardDistributionPolicy.CalculateStrategicRemainingCapacity(20, 20, 3, 1, 10));
    Equal(30L, ScratchcardDistributionPolicy.CalculateStrategicRemainingCapacity(20, 35, 3, 1, 10));
    Equal(0L, ScratchcardDistributionPolicy.CalculateStrategicRemainingCapacity(20, 20, 0, 1, 10));
    return Task.CompletedTask;
}

static Task TestWinnerProbabilityAsync()
{
    var neutral = new ScratchcardDistributionState(999, 100, 100, 60, 0, 100, 1001);
    var deficit = neutral with { AssignedWinners = 60, RemainingPrizes = 140 };
    var surplus = neutral with { AssignedWinners = 120, RemainingPrizes = 80 };
    var firstWinner = neutral with { UserAssignedWinners = 1 };
    var severalWinners = neutral with { UserAssignedWinners = 3 };

    var neutralProbability = ScratchcardDistributionPolicy.CalculateWinnerProbability(1, 10, neutral);
    True(
        ScratchcardDistributionPolicy.CalculateWinnerProbability(1, 10, deficit) > neutralProbability,
        "Un déficit global debe aumentar moderadamente la probabilidad.");
    True(
        ScratchcardDistributionPolicy.CalculateWinnerProbability(1, 10, surplus) < neutralProbability,
        "Un superávit global debe reducir la probabilidad.");
    True(
        neutralProbability >
        ScratchcardDistributionPolicy.CalculateWinnerProbability(1, 10, firstWinner),
        "Un usuario sin premio debe tener mayor peso que uno con un premio.");
    True(
        ScratchcardDistributionPolicy.CalculateWinnerProbability(1, 10, firstWinner) >
        ScratchcardDistributionPolicy.CalculateWinnerProbability(1, 10, severalWinners),
        "La prioridad debe disminuir progresivamente.");
    True(neutralProbability < 1m, "La configuración 1/10 no debe garantizar un premio.");
    True(
        ScratchcardDistributionPolicy.CalculateWinnerProbability(2, 20, neutral) > 0,
        "Una configuración diferente debe conservar oportunidad de ganar.");
    return Task.CompletedTask;
}

static Task TestWeightedPrizeSelectionAsync()
{
    Equal(0, ScratchcardDistributionPolicy.SelectWeightedIndex(new[] { 2, 3, 0 }, 0));
    Equal(0, ScratchcardDistributionPolicy.SelectWeightedIndex(new[] { 2, 3, 0 }, 1));
    Equal(1, ScratchcardDistributionPolicy.SelectWeightedIndex(new[] { 2, 3, 0 }, 2));
    Equal(1, ScratchcardDistributionPolicy.SelectWeightedIndex(new[] { 0, 1, 0 }, 0));
    Equal(-1, ScratchcardDistributionPolicy.SelectWeightedIndex(new[] { 0, 0, 0 }, 0));
    return Task.CompletedTask;
}

static Task TestPrizeEligibilityPolicyAsync()
{
    WalletTransaction Deposit(
        int id,
        decimal amount,
        DateTime? completedpt = null,
        WalletTransactionType type = WalletTransactionType.Deposit,
        WalletTransactionStatus status = WalletTransactionStatus.Completed,
        string paymentIntentId = "pi_test",
        int userId = 7) => new()
        {
            Id = id,
            UserId = userId,
            Type = type,
            Amount = amount,
            Status = status,
            StripePaymentIntentId = paymentIntentId,
            StripeEventId = completedpt.HasValue &&
                status == WalletTransactionStatus.Completed &&
                type == WalletTransactionType.Deposit &&
                !string.IsNullOrWhiteSpace(paymentIntentId)
                    ? $"evt_{id}"
                    : null,
            CompletedAt = completedpt
        };

    WalletTransaction Current(int id, decimal amount) =>
        Deposit(id, amount, null, status: WalletTransactionStatus.Pending);

    var now = ApplicationClock.Now;
    Equal(
        100m,
        ScratchcardPrizeEligibilityPolicy.CalculateRecentConfirmedStripeDepositTotal(
            Current(10, 100m),
            Array.Empty<WalletTransaction>()));
    Equal(
        200m,
        ScratchcardPrizeEligibilityPolicy.CalculateRecentConfirmedStripeDepositTotal(
            Current(10, 100m),
            new[] { Deposit(1, 100m, now.AddMinutes(-1)) }));
    Equal(
        400m,
        ScratchcardPrizeEligibilityPolicy.CalculateRecentConfirmedStripeDepositTotal(
            Current(10, 200m),
            new[] { Deposit(1, 200m, now.AddMinutes(-1)) }));
    Equal(
        450m,
        ScratchcardPrizeEligibilityPolicy.CalculateRecentConfirmedStripeDepositTotal(
            Current(10, 200m),
            new[] { Deposit(1, 250m, now.AddMinutes(-1)) }));
    Equal(
        600m,
        ScratchcardPrizeEligibilityPolicy.CalculateRecentConfirmedStripeDepositTotal(
            Current(10, 300m),
            new[] { Deposit(1, 300m, now.AddMinutes(-1)) }));
    Equal(
        400m,
        ScratchcardPrizeEligibilityPolicy.CalculateRecentConfirmedStripeDepositTotal(
            Current(10, 300m),
            new[]
            {
                Deposit(1, 100m, now.AddMinutes(-2)),
                Deposit(2, 100m, now.AddMinutes(-1))
            }),
        "El historial 100, 100, 300 sólo debe usar 100 + 300.");
    Equal(
        600m,
        ScratchcardPrizeEligibilityPolicy.CalculateRecentConfirmedStripeDepositTotal(
            Current(10, 300m),
            new[]
            {
                Deposit(1, 100m, now.AddMinutes(-2)),
                Deposit(2, 300m, now.AddMinutes(-1))
            }),
        "El historial 100, 300, 300 sólo debe usar 300 + 300.");

    var ignoredMovements = new[]
    {
        Deposit(1, 100m, now.AddMinutes(-10)),
        Deposit(2, 900m, now.AddMinutes(-1), WalletTransactionType.ScratchcardPrize),
        Deposit(3, 900m, now.AddMinutes(-1), WalletTransactionType.Adjustment),
        Deposit(4, 900m, now.AddMinutes(-1), WalletTransactionType.Purchase),
        Deposit(5, 900m, now.AddMinutes(-1), status: WalletTransactionStatus.Pending),
        Deposit(6, 900m, now.AddMinutes(-1), status: WalletTransactionStatus.Failed),
        Deposit(7, 900m, now.AddMinutes(-1), paymentIntentId: null),
        Deposit(8, 900m, now.AddMinutes(-1), userId: 99)
    };
    Equal(
        200m,
        ScratchcardPrizeEligibilityPolicy.CalculateRecentConfirmedStripeDepositTotal(
            Current(10, 100m),
            ignoredMovements),
        "Premios, bonos, compras y movimientos no confirmados no deben contar.");

    Equal(50m, ScratchcardPrizeEligibilityPolicy.GetPreferredMaximumPrize(100m));
    Equal(50m, ScratchcardPrizeEligibilityPolicy.GetPreferredMaximumPrize(200m));
    Equal(100m, ScratchcardPrizeEligibilityPolicy.GetPreferredMaximumPrize(201m));
    Equal(100m, ScratchcardPrizeEligibilityPolicy.GetPreferredMaximumPrize(400m));
    Equal(200m, ScratchcardPrizeEligibilityPolicy.GetPreferredMaximumPrize(401m));
    Equal(200m, ScratchcardPrizeEligibilityPolicy.GetPreferredMaximumPrize(500m));
    Equal<decimal?>(null, ScratchcardPrizeEligibilityPolicy.GetPreferredMaximumPrize(501m));

    var amounts = new[] { 50m, 100m, 200m, 300m };
    True(
        ScratchcardPrizeEligibilityPolicy.GetEligiblePrizeIndexes(
            amounts, new[] { 70, 60, 30, 15 }, 200m).SequenceEqual(new[] { 0 }),
        "Hasta 200 sólo debe elegir $50 mientras exista.");
    True(
        ScratchcardPrizeEligibilityPolicy.GetEligiblePrizeIndexes(
            amounts, new[] { 70, 60, 30, 15 }, 400m).SequenceEqual(new[] { 0, 1 }),
        "Hasta 400 debe elegir $50/$100.");
    True(
        ScratchcardPrizeEligibilityPolicy.GetEligiblePrizeIndexes(
            amounts, new[] { 70, 60, 30, 15 }, 450m).SequenceEqual(new[] { 0, 1, 2 }),
        "Hasta 500 debe elegir $50/$100/$200.");
    True(
        ScratchcardPrizeEligibilityPolicy.GetEligiblePrizeIndexes(
            amounts, new[] { 70, 60, 30, 15 }, 600m).SequenceEqual(new[] { 0, 1, 2, 3 }),
        "aesde 501 debe permitir cualquier premio disponible.");

    True(
        ScratchcardPrizeEligibilityPolicy.GetEligiblePrizeIndexes(
            amounts, new[] { 0, 60, 30, 15 }, 200m).SequenceEqual(new[] { 1 }),
        "pgotado $50 debe escalar a $100.");
    True(
        ScratchcardPrizeEligibilityPolicy.GetEligiblePrizeIndexes(
            amounts, new[] { 0, 0, 30, 15 }, 200m).SequenceEqual(new[] { 2 }),
        "pgotados $50/$100 debe escalar a $200.");
    True(
        ScratchcardPrizeEligibilityPolicy.GetEligiblePrizeIndexes(
            amounts, new[] { 0, 0, 0, 15 }, 200m).SequenceEqual(new[] { 3 }),
        "pgotados $50/$100/$200 debe escalar a $300.");
    Equal(
        0,
        ScratchcardPrizeEligibilityPolicy.GetEligiblePrizeIndexes(
            amounts, new[] { 0, 0, 0, 0 }, 200m).Count,
        "Inventario agotado no debe inventar premios.");

    Equal(
        3L,
        ScratchcardDistributionPolicy.CalculateScratchcards(100m, 30m),
        "El total reciente no debe alterar los rascaditos del depósito actual.");
    return Task.CompletedTask;
}

static async Task TestPrizeEligibilityAcrossTimeZonesAsync()
{
    var options = CreateOptions($"eLottoDepositOrder_{Guid.NewGuid():N}");
    try
    {
        var seeded = await RecreateAndSeedAsync(options, prizeQuantity: 1);
        ISorteoTimeService timeService = new SorteoTimeService();
        await using var context = new eLottoContext(options);
        var cancun = await context.Sorteos.SingleAsync(x => x.Id == seeded.SorteoId);
        cancun.ZonaHoraria = "Cancun";
        var tijuana = new Sorteos
        {
            Nombre = "Sorteo Tijuana",
            Imagen1 = "tijuana.webp",
            Fecha = new DateTime(2026, 10, 1, 12, 0, 0),
            ZonaHoraria = "Tijuana",
            PrecioBoleto = 1,
            PrecioPorMil = 1,
            CantidadBoletos = 100
        };
        context.Sorteos.Add(tijuana);

        var olderMoment = new DateTimeOffset(2026, 9, 29, 16, 0, 0, TimeSpan.Zero);
        var newerMoment = olderMoment.AddMinutes(5);
        context.WalletTransactions.AddRange(
            new WalletTransaction
            {
                UserId = seeded.UserId,
                WalletId = seeded.WalletId,
                Sorteo = cancun,
                Type = WalletTransactionType.Deposit,
                Amount = 200m,
                Status = WalletTransactionStatus.Completed,
                StripePaymentIntentId = "pi_older_cancun",
                StripeEventId = "evt_older_cancun",
                CreatedAt = timeService.ConvertToSorteoTime(olderMoment, cancun).DateTime,
                CompletedAt = timeService.ConvertToSorteoTime(olderMoment, cancun).DateTime
            },
            new WalletTransaction
            {
                UserId = seeded.UserId,
                WalletId = seeded.WalletId,
                Sorteo = tijuana,
                Type = WalletTransactionType.Deposit,
                Amount = 300m,
                Status = WalletTransactionStatus.Completed,
                StripePaymentIntentId = "pi_newer_tijuana",
                StripeEventId = "evt_newer_tijuana",
                CreatedAt = timeService.ConvertToSorteoTime(newerMoment, tijuana).DateTime,
                CompletedAt = timeService.ConvertToSorteoTime(newerMoment, tijuana).DateTime
            });
        await context.SaveChangesAsync();

        var current = await context.WalletTransactions
            .SingleAsync(x => x.Id == seeded.TransactionId);
        Equal(600m,
            await ScratchcardPrizeEligibilityPolicy.CalculateRecentConfirmedStripeDepositTotalAsync(
                context.WalletTransactions, current, timeService, CancellationToken.None),
            "Debe sumar el depósito más reciente en tiempo real, aunque su hora local sea menor.");
    }
    finally { await DeleteDatabaseAsync(options); }
}

static async Task TestPrizeEligibilityIntegrationAsync()
{
    var databaseName = $"eLottoScratchcardTests_{Guid.NewGuid():N}";
    var options = CreateOptions(databaseName);
    try
    {
        var seeded = await RecreateAndSeedAsync(
            options,
            prizeQuantity: 1000,
            winnersPerGroup: 1,
            scratchcardsPerGroup: 1,
            amountPerScratchcard: 10m,
            depositAmount: 100m);

        await using (var configuration = new eLottoContext(options))
        {
            configuration.SorteosRascaditoPremios.AddRange(
                new SorteosRascaditoPremios { SorteosId = seeded.SorteoId, Premio = 100m, Cantidad = 1000 },
                new SorteosRascaditoPremios { SorteosId = seeded.SorteoId, Premio = 200m, Cantidad = 1000 },
                new SorteosRascaditoPremios { SorteosId = seeded.SorteoId, Premio = 300m, Cantidad = 1000 });
            configuration.WalletTransactions.AddRange(
                new WalletTransaction
                {
                    UserId = seeded.UserId,
                    WalletId = seeded.WalletId,
                    Type = WalletTransactionType.ScratchcardPrize,
                    Amount = 1000m,
                    Status = WalletTransactionStatus.Completed,
                    StripePaymentIntentId = "pi_should_not_count_prize",
                    CreatedAt = ApplicationClock.Now.AddMinutes(-2),
                    CompletedAt = ApplicationClock.Now.AddMinutes(-2)
                },
                new WalletTransaction
                {
                    UserId = seeded.UserId,
                    WalletId = seeded.WalletId,
                    Type = WalletTransactionType.Adjustment,
                    Amount = 1000m,
                    Status = WalletTransactionStatus.Completed,
                    StripePaymentIntentId = "pi_should_not_count_bonus",
                    CreatedAt = ApplicationClock.Now.AddMinutes(-1),
                    CompletedAt = ApplicationClock.Now.AddMinutes(-1)
                });
            await configuration.SaveChangesAsync();
        }

        await using (var historyVerification = new eLottoContext(options))
        {
            var currentDeposit = await historyVerification.WalletTransactions
                .SingleAsync(x => x.Id == seeded.TransactionId);
            Equal(
                100m,
                await ScratchcardPrizeEligibilityPolicy.CalculateRecentConfirmedStripeDepositTotalAsync(
                    historyVerification.WalletTransactions,
                    currentDeposit,
                    new SorteoTimeService(),
                    CancellationToken.None),
                "La consulta SQL debe excluir premios y ajustes completados.");
        }

        await ProcessEventAsync(
            options,
            CreateSucceededEvent(
                "evt_prize_eligibility_first",
                seeded.PaymentIntentId,
                seeded.TransactionId,
                seeded.UserId,
                100m));

        Dictionary<int, string> frozenResults;
        await using (var firstVerification = new eLottoContext(options))
        {
            var firstBatch = await firstVerification.SorteosRascaditos
                .AsNoTracking()
                .Where(x => x.WalletTransactionOrigenId == seeded.TransactionId)
                .ToListAsync();
            Equal(10, firstBatch.Count);
            True(firstBatch.Any(x => x.EsGanador), "El lote de prueba debe contener ganadores.");
            True(
                firstBatch.Where(x => x.EsGanador).All(x => x.ImportePremio == 50m),
                "Con $100, un ganador debe recibir $50 mientras exista inventario.");
            frozenResults = firstBatch.ToDictionary(
                x => x.Id,
                x => $"{x.EsGanador}|{x.SorteosRascaditoPremioId}|{x.ImportePremio}|{x.MatrizResultado}|{x.LineaGanadora}");
        }

        int losingScratchcardId;
        await using (var visualSetup = new eLottoContext(options))
        {
            var prizes = await visualSetup.SorteosRascaditoPremios
                .AsNoTracking()
                .Where(x => x.SorteosId == seeded.SorteoId)
                .OrderBy(x => x.Id)
                .Select(x => x.Cantidad)
                .ToArrayAsync();
            var folio = FindFolioForWeightedPrize(prizes, 3);
            var losingScratchcard = CreateLosingScratchcard(seeded, folio);
            visualSetup.SorteosRascaditos.Add(losingScratchcard);
            await visualSetup.SaveChangesAsync();
            losingScratchcardId = losingScratchcard.Id;
        }

        var losingPreview = await StartWithNewContextAsync(
            options,
            losingScratchcardId,
            seeded.UserId);
        Equal(
            300m,
            losingPreview.PremioPosible,
            "Una perdedora de rango bajo debe poder representar visualmente $300.");

        var secondDeposit = await AddPendingDepositAsync(
            options,
            seeded.UserId,
            seeded.WalletId,
            seeded.SorteoId,
            300m);
        await using (var secondHistoryVerification = new eLottoContext(options))
        {
            var currentDeposit = await secondHistoryVerification.WalletTransactions
                .SingleAsync(x => x.Id == secondDeposit.TransactionId);
            Equal(
                400m,
                await ScratchcardPrizeEligibilityPolicy.CalculateRecentConfirmedStripeDepositTotalAsync(
                    secondHistoryVerification.WalletTransactions,
                    currentDeposit,
                    new SorteoTimeService(),
                    CancellationToken.None),
                "El segundo lote debe usar únicamente 100 + 300.");
        }

        await ProcessEventAsync(
            options,
            CreateSucceededEvent(
                "evt_prize_eligibility_second",
                secondDeposit.PaymentIntentId,
                secondDeposit.TransactionId,
                seeded.UserId,
                300m));

        await using var finalVerification = new eLottoContext(options);
        Equal(
            30,
            await finalVerification.SorteosRascaditos.CountAsync(
                x => x.WalletTransactionOrigenId == secondDeposit.TransactionId),
            "El segundo depósito debe generar sólo 300/10 rascaditos, no por la suma reciente.");
        foreach (var frozen in frozenResults)
        {
            var stored = await finalVerification.SorteosRascaditos
                .AsNoTracking()
                .SingleAsync(x => x.Id == frozen.Key);
            Equal(
                frozen.Value,
                $"{stored.EsGanador}|{stored.SorteosRascaditoPremioId}|{stored.ImportePremio}|{stored.MatrizResultado}|{stored.LineaGanadora}",
                "Un depósito posterior no debe recalcular rascaditos anteriores.");
        }
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}

static Task TestWinningVisualMatricesAsync()
{
    foreach (var lineIndex in Enumerable.Range(0, 8))
    {
        var result = ScratchcardVisualResultGenerator.Generate(true, lineIndex);
        True(
            ScratchcardVisualResultGenerator.IsValid(result, true),
            $"La línea {lineIndex} debe producir una matriz ganadora válida.");
        Equal(3, ScratchcardVisualResultGenerator.GetWinningCells(result.WinningLine).Count);
    }

    return Task.CompletedTask;
}

static Task TestLosingVisualMatricesAsync()
{
    for (var attempt = 0; attempt < 250; attempt++)
    {
        var result = ScratchcardVisualResultGenerator.Generate(false);
        True(
            ScratchcardVisualResultGenerator.IsValid(result, false),
            "Una matriz perdedora no debe contener ninguna línea ganadora.");
    }

    return Task.CompletedTask;
}

static async Task TestSafeScratchcardListAsync()
{
    var databaseName = $"eLottoScratchcardTests_{Guid.NewGuid():N}";
    var options = CreateOptions(databaseName);
    try
    {
        var seeded = await RecreateAndSeedAsync(options, prizeQuantity: 5);
        var pendingId = await AddScratchcardAsync(options, seeded, isWinner: false);
        var revealedId = await AddScratchcardAsync(options, seeded, isWinner: false);
        await RevealWithNewContextAsync(options, revealedId, seeded.UserId);

        await using var context = new eLottoContext(options);
        var service = new ScratchcardRevealService(
            context,
            NullLogger<ScratchcardRevealService>.Instance,
            new SorteoTimeService());
        var response = await service.GetListAsync(
            seeded.UserId,
            CancellationToken.None);

        Equal(1, response.Pendientes);
        Equal(2, response.Rascaditos.Count);
        Equal(pendingId, response.Rascaditos[0].Id);
        True(!response.Rascaditos[0].Revelado, "Los pendientes deben aparecer primero.");
        True(response.Rascaditos[1].Revelado, "El histórico debe conservar su estado.");
        var publicFields = typeof(ScratchcardListItemResponse)
            .GetProperties()
            .Select(x => x.Name)
            .ToArray();
        Equal(5, publicFields.Length);
        True(
            !publicFields.Intersect(new[]
            {
                "EsGanador",
                "ImportePremio",
                "SorteosRascaditoPremioId",
                "MatrizResultado",
                "LineaGanadora",
                "WalletTransactionPremioId"
            }).Any(),
            "El DTO de listado no debe filtrar ningún dato oculto.");
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}

static async Task TestUserPrizeHistoryAsync()
{
    var databaseName = $"eLottoScratchcardTests_{Guid.NewGuid():N}";
    var options = CreateOptions(databaseName);
    try
    {
        var seeded = await RecreateAndSeedAsync(options, prizeQuantity: 5);
        var winnerId = await AddScratchcardAsync(
            options,
            seeded,
            isWinner: true,
            prizeAmount: 50m);
        await RevealWithNewContextAsync(options, winnerId, seeded.UserId);

        await using (var setupContext = new eLottoContext(options))
        {
            var sorteo = await setupContext.Sorteos.SingleAsync(x => x.Id == seeded.SorteoId);
            sorteo.UsuarioIdGanador = seeded.UserId;
            sorteo.NumeroGanador = "00042";
            sorteo.Fecha = ApplicationClock.Now.AddDays(-1);
            await setupContext.SaveChangesAsync();
        }

        await using var context = new eLottoContext(options);
        var service = new UserPrizeHistoryService(context, new SorteoTimeService());
        var response = await service.GetAsync(seeded.UserId, CancellationToken.None);

        Equal(2, response.TotalPremios);
        Equal(1, response.TotalSorteos);
        Equal(1, response.TotalRascaditos);
        Equal(50m, response.ImporteRascaditos);
        Equal("rascadito", response.Premios[0].Tipo);
        Equal(winnerId, response.Premios[0].RascaditoId!.Value);
        Equal(9, response.Premios[0].MatrizResultado.Count);
        True(
            !string.IsNullOrWhiteSpace(response.Premios[0].LineaGanadora),
            "El historial debe conservar la línea ganadora visual.");
        Equal("sorteo", response.Premios[1].Tipo);
        Equal("00042", response.Premios[1].NumeroGanador);
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}
static async Task TestLargeTicketInitializationAsync()
{
    var databaseName = $"eLottoScratchcardTests_{Guid.NewGuid():N}";
    var options = CreateOptions(databaseName);
    try
    {
        var seeded = await RecreateAndSeedAsync(options, prizeQuantity: 1);
        await using (var setupContext = new eLottoContext(options))
        {
            var sorteo = await setupContext.Sorteos.SingleAsync(x => x.Id == seeded.SorteoId);
            sorteo.CantidadBoletos = 600000;
            await setupContext.SaveChangesAsync();
        }

        async Task InitializeAsync()
        {
            await using var context = new eLottoContext(options);
            var sorteo = await context.Sorteos.AsNoTracking()
                .SingleAsync(x => x.Id == seeded.SorteoId);
            var repository = new UserLotteryRepository(context);
            await repository.EnsureTicketsAsync(sorteo, CancellationToken.None);
        }

        await Task.WhenAll(InitializeAsync(), InitializeAsync());

        await using var verificationContext = new eLottoContext(options);
        var tickets = verificationContext.SorteosBoletos
            .AsNoTracking()
            .Where(x => x.SorteosId == seeded.SorteoId);
        Equal(600000, await tickets.CountAsync());
        Equal("000000", await tickets.MinAsync(x => x.Numero));
        Equal("599999", await tickets.MaxAsync(x => x.Numero));
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}
static async Task TestDatabaseInvariantsAsync()
{
    var databaseName = $"eLottoScratchcardTests_{Guid.NewGuid():N}";
    var options = CreateOptions(databaseName);
    try
    {
        var generatedFolio = ScratchcardFolioGenerator.Create();
        Equal(16, generatedFolio.Length);
        True(
            generatedFolio.All(Uri.IsHexDigit),
            "El folio corto del rascadito debe conservar únicamente caracteres hexadecimales.");

        var seeded = await RecreateAndSeedAsync(options, prizeQuantity: 1);

        await using (var invalidContext = new eLottoContext(options))
        {
            invalidContext.SorteosRascaditos.Add(new SorteosRascaditos
            {
                SorteosId = seeded.SorteoId,
                UsuarioId = seeded.UserId,
                WalletTransactionOrigenId = seeded.TransactionId,
                Folio = ScratchcardFolioGenerator.Create(),
                EsGanador = true,
                Revelado = false,
                FechaGeneracion = ApplicationClock.Now
            });
            await ThrowsAsync<DbUpdateException>(() => invalidContext.SaveChangesAsync());
        }

        var duplicateFolio = ScratchcardFolioGenerator.Create();
        await using (var duplicateContext = new eLottoContext(options))
        {
            duplicateContext.SorteosRascaditos.AddRange(
                CreateLosingScratchcard(seeded, duplicateFolio),
                CreateLosingScratchcard(seeded, duplicateFolio));
            await ThrowsAsync<DbUpdateException>(() => duplicateContext.SaveChangesAsync());
        }

        var start = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<int> ReserveLastPrizeAsync()
        {
            await using var context = new eLottoContext(options);
            await using var transaction = await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);
            await start.Task;
            var affected = await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [SorteosRascaditoPremios] SET [Entregados] = [Entregados] + 1 WHERE [Id] = {seeded.PrizeId} AND [Entregados] < [Cantidad]");
            await transaction.CommitAsync();
            return affected;
        }

        var contenders = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(ReserveLastPrizeAsync))
            .ToArray();
        start.SetResult();
        var reservations = await Task.WhenAll(contenders);

        Equal(1, reservations.Sum());
        await using var verificationContext = new eLottoContext(options);
        var prize = await verificationContext.SorteosRascaditoPremios
            .AsNoTracking()
            .SingleAsync(x => x.Id == seeded.PrizeId);
        Equal(1, prize.Entregados);
        True(prize.Entregados <= prize.Cantidad, "Entregados nunca debe superar Cantidad.");
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}

static async Task TestStripeIdempotencyAsync()
{
    var databaseName = $"eLottoScratchcardTests_{Guid.NewGuid():N}";
    var options = CreateOptions(databaseName);
    try
    {
        var seeded = await RecreateAndSeedAsync(
            options,
            prizeQuantity: 10,
            winnersPerGroup: 1,
            scratchcardsPerGroup: 10,
            amountPerScratchcard: 30m,
            depositAmount: 300m);
        var firstEvent = CreateSucceededEvent(
            "evt_scratchcards_first",
            seeded.PaymentIntentId,
            seeded.TransactionId,
            seeded.UserId,
            300m);

        await ProcessEventAsync(options, firstEvent);
        var firstResult = await ReadResultAsync(options, seeded);
        Equal(10, firstResult.Scratchcards);
        Equal(300m, firstResult.Balance);
        Equal(WalletTransactionStatus.Completed, firstResult.Status);
        Equal(firstResult.Winners, firstResult.Delivered);
        True(firstResult.AllUnrevealed, "Todos los rascaditos deben nacer sin revelar.");
        True(firstResult.AllResultsCoherent, "Todos los resultados deben nacer definitivos y coherentes.");

        await ProcessEventAsync(options, firstEvent);
        await ProcessEventAsync(
            options,
            CreateSucceededEvent(
                "evt_scratchcards_retry",
                seeded.PaymentIntentId,
                seeded.TransactionId,
                seeded.UserId,
                300m));

        var retryResult = await ReadResultAsync(options, seeded);
        Equal(firstResult.Scratchcards, retryResult.Scratchcards);
        Equal(firstResult.Delivered, retryResult.Delivered);
        Equal(firstResult.Balance, retryResult.Balance);
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}

static async Task TestOriginBatchIdempotencyAsync()
{
    var databaseName = $"eLottoScratchcardTests_{Guid.NewGuid():N}";
    var options = CreateOptions(databaseName);
    try
    {
        var seeded = await RecreateAndSeedAsync(
            options,
            prizeQuantity: 10,
            depositAmount: 300m);

        await using var context = new eLottoContext(options);
        await using var dbTransaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable);
        var transaction = await context.WalletTransactions
            .SingleAsync(x => x.Id == seeded.TransactionId);
        var service = new ScratchcardAssignmentService(
            context,
            NullLogger<ScratchcardAssignmentService>.Instance,
            new SorteoTimeService());

        Equal(10, await service.AssignForConfirmedStripeDepositAsync(
            transaction,
            CancellationToken.None));
        Equal(0, await service.AssignForConfirmedStripeDepositAsync(
            transaction,
            CancellationToken.None));
        Equal(0, await service.AssignForConfirmedStripeDepositAsync(
            new WalletTransaction { Type = WalletTransactionType.Adjustment },
            CancellationToken.None));
        await context.SaveChangesAsync();
        await dbTransaction.CommitAsync();

        await using var verification = new eLottoContext(options);
        Equal(
            10,
            await verification.SorteosRascaditos.CountAsync(
                x => x.WalletTransactionOrigenId == seeded.TransactionId));
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}

static async Task TestNonGeneratingDepositsAsync()
{
    var databaseName = $"eLottoScratchcardTests_{Guid.NewGuid():N}";
    var options = CreateOptions(databaseName);
    try
    {
        var seeded = await RecreateAndSeedAsync(
            options,
            prizeQuantity: 10,
            depositAmount: 29.99m);

        await ProcessEventAsync(
            options,
            CreateSucceededEvent(
                "evt_below_minimum",
                seeded.PaymentIntentId,
                seeded.TransactionId,
                seeded.UserId,
                29.99m));

        await using (var belowMinimum = new eLottoContext(options))
        {
            Equal(0, await belowMinimum.SorteosRascaditos.CountAsync());
            Equal(
                29.99m,
                await belowMinimum.UserWallets
                    .Where(x => x.Id == seeded.WalletId)
                    .Select(x => x.Balance)
                    .SingleAsync());
        }

        await using (var configuration = new eLottoContext(options))
        {
            var lottery = await configuration.Sorteos.SingleAsync(
                x => x.Id == seeded.SorteoId);
            lottery.RascaditosHabilitados = false;
            lottery.GanadoresPorGrupo = 0;
            lottery.RascaditosPorGrupo = 0;
            lottery.ImporteDepositoStripePorRascadito = 0;
            await configuration.SaveChangesAsync();
        }

        var disabledDeposit = await AddPendingDepositAsync(
            options,
            seeded.UserId,
            seeded.WalletId,
            seeded.SorteoId,
            300m);
        await ProcessEventAsync(
            options,
            CreateSucceededEvent(
                "evt_disabled",
                disabledDeposit.PaymentIntentId,
                disabledDeposit.TransactionId,
                seeded.UserId,
                300m));

        var pendingDeposit = await AddPendingDepositAsync(
            options,
            seeded.UserId,
            seeded.WalletId,
            seeded.SorteoId,
            300m);
        var irrelevant = CreateSucceededEvent(
            "evt_irrelevant",
            pendingDeposit.PaymentIntentId,
            pendingDeposit.TransactionId,
            seeded.UserId,
            300m);
        irrelevant.Type = "payment_intent.created";
        await ProcessEventAsync(options, irrelevant);

        await using (var pendingVerification = new eLottoContext(options))
        {
            Equal(
                WalletTransactionStatus.Pending,
                await pendingVerification.WalletTransactions
                    .Where(x => x.Id == pendingDeposit.TransactionId)
                    .Select(x => x.Status)
                    .SingleAsync());
            Equal(
                0,
                await pendingVerification.SorteosRascaditos.CountAsync(
                    x => x.WalletTransactionOrigenId == pendingDeposit.TransactionId));
        }

        var failed = CreateSucceededEvent(
            "evt_failed",
            pendingDeposit.PaymentIntentId,
            pendingDeposit.TransactionId,
            seeded.UserId,
            300m);
        failed.Type = "payment_intent.payment_failed";
        await ProcessEventAsync(options, failed);

        await using var finalVerification = new eLottoContext(options);
        Equal(
            WalletTransactionStatus.Failed,
            await finalVerification.WalletTransactions
                .Where(x => x.Id == pendingDeposit.TransactionId)
                .Select(x => x.Status)
                .SingleAsync());
        Equal(0, await finalVerification.SorteosRascaditos.CountAsync());
        Equal(
            329.99m,
            await finalVerification.UserWallets
                .Where(x => x.Id == seeded.WalletId)
                .Select(x => x.Balance)
                .SingleAsync());
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}

static async Task TestDynamicConfigurationAsync()
{
    var databaseName = $"eLottoScratchcardTests_{Guid.NewGuid():N}";
    var options = CreateOptions(databaseName);
    try
    {
        var seeded = await RecreateAndSeedAsync(
            options,
            prizeQuantity: 10,
            winnersPerGroup: 2,
            scratchcardsPerGroup: 15,
            amountPerScratchcard: 50m,
            depositAmount: 300m);

        await using (var configuration = new eLottoContext(options))
        {
            var firstPrize = await configuration.SorteosRascaditoPremios
                .SingleAsync(x => x.Id == seeded.PrizeId);
            firstPrize.Premio = 75m;
            configuration.SorteosRascaditoPremios.AddRange(
                new SorteosRascaditoPremios
                {
                    SorteosId = seeded.SorteoId,
                    Premio = 150m,
                    Cantidad = 5
                },
                new SorteosRascaditoPremios
                {
                    SorteosId = seeded.SorteoId,
                    Premio = 500m,
                    Cantidad = 2
                });
            await configuration.SaveChangesAsync();
        }

        await ProcessEventAsync(
            options,
            CreateSucceededEvent(
                "evt_dynamic_configuration",
                seeded.PaymentIntentId,
                seeded.TransactionId,
                seeded.UserId,
                300m));

        await using var verification = new eLottoContext(options);
        var scratchcards = await verification.SorteosRascaditos
            .AsNoTracking()
            .Where(x => x.SorteosId == seeded.SorteoId)
            .ToListAsync();
        Equal(6, scratchcards.Count);
        Equal(
            127L,
            ScratchcardDistributionPolicy.CalculateCapacity(17, 2, 15));
        True(
            scratchcards
                .Where(x => x.EsGanador)
                .All(x => x.ImportePremio is 75m or 150m or 500m),
            "Los ganadores sólo deben usar denominaciones configuradas.");

        var delivered = await verification.SorteosRascaditoPremios
            .Where(x => x.SorteosId == seeded.SorteoId)
            .SumAsync(x => x.Entregados);
        Equal(scratchcards.Count(x => x.EsGanador), delivered);
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}

static async Task TestConcurrentDepositCapacityAsync()
{
    var databaseName = $"eLottoScratchcardTests_{Guid.NewGuid():N}";
    var options = CreateOptions(databaseName);
    try
    {
        var seeded = await RecreateAndSeedAsync(
            options,
            prizeQuantity: 20,
            winnersPerGroup: 1,
            scratchcardsPerGroup: 6,
            amountPerScratchcard: 30m,
            depositAmount: 300m);
        await ProcessEventAsync(
            options,
            CreateSucceededEvent(
                "evt_capacity_initial",
                seeded.PaymentIntentId,
                seeded.TransactionId,
                seeded.UserId,
                300m));

        await using (var setup = new eLottoContext(options))
        {
            setup.SorteosRascaditos.AddRange(
                Enumerable.Range(0, 110).Select(_ => new SorteosRascaditos
                {
                    SorteosId = seeded.SorteoId,
                    UsuarioId = seeded.UserId,
                    WalletTransactionOrigenId = seeded.TransactionId,
                    Folio = ScratchcardFolioGenerator.Create(),
                    FechaGeneracion = ApplicationClock.Now
                }));
            await setup.SaveChangesAsync();
        }

        var left = await AddPendingDepositAsync(
            options,
            seeded.UserId,
            seeded.WalletId,
            seeded.SorteoId,
            300m);
        var right = await AddPendingDepositAsync(
            options,
            seeded.UserId,
            seeded.WalletId,
            seeded.SorteoId,
            300m);
        var start = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        async Task ProcessConcurrentlyAsync(
            string eventId,
            PendingDeposit deposit)
        {
            await start.Task;
            await ProcessEventAsync(
                options,
                CreateSucceededEvent(
                    eventId,
                    deposit.PaymentIntentId,
                    deposit.TransactionId,
                    seeded.UserId,
                    300m));
        }

        var leftTask = Task.Run(() => ProcessConcurrentlyAsync(
            "evt_capacity_left",
            left));
        var rightTask = Task.Run(() => ProcessConcurrentlyAsync(
            "evt_capacity_right",
            right));
        start.SetResult();
        await Task.WhenAll(leftTask, rightTask);

        await using var verification = new eLottoContext(options);
        var generated = await verification.SorteosRascaditos
            .Where(x => x.SorteosId == seeded.SorteoId)
            .CountAsync();
        True(generated > 120 && generated <= 140,
            "Los depósitos deben generar después de la meta de 120 mientras queden premios.");
        Equal(
            900m,
            await verification.UserWallets
                .Where(x => x.Id == seeded.WalletId)
                .Select(x => x.Balance)
                .SingleAsync());
        Equal(
            2,
            await verification.WalletTransactions.CountAsync(
                x => (x.Id == left.TransactionId || x.Id == right.TransactionId) &&
                    x.Status == WalletTransactionStatus.Completed));

        var winners = await verification.SorteosRascaditos
            .CountAsync(x => x.SorteosId == seeded.SorteoId && x.EsGanador);
        var delivered = await verification.SorteosRascaditoPremios
            .Where(x => x.SorteosId == seeded.SorteoId)
            .SumAsync(x => x.Entregados);
        Equal(winners, delivered);
        True(delivered <= 20, "Los depósitos concurrentes no deben exceder inventario.");
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}

static async Task TestExhaustedScratchcardInventoryAsync()
{
    var options = CreateOptions($"eLottoScratchcardTests_{Guid.NewGuid():N}");
    try
    {
        var seeded = await RecreateAndSeedAsync(options, prizeQuantity: 1);
        await AddScratchcardAsync(options, seeded, isWinner: true, prizeAmount: 50m);

        var deposit = await AddPendingDepositAsync(
            options,
            seeded.UserId,
            seeded.WalletId,
            seeded.SorteoId,
            300m);
        await ProcessEventAsync(
            options,
            CreateSucceededEvent(
                "evt_inventory_exhausted",
                deposit.PaymentIntentId,
                deposit.TransactionId,
                seeded.UserId,
                300m));

        await using var verification = new eLottoContext(options);
        Equal(0, await verification.SorteosRascaditos.CountAsync(
            x => x.WalletTransactionOrigenId == deposit.TransactionId));
        Equal(WalletTransactionStatus.Completed,
            await verification.WalletTransactions
                .Where(x => x.Id == deposit.TransactionId)
                .Select(x => x.Status)
                .SingleAsync());
        Equal(300m, await verification.UserWallets
            .Where(x => x.Id == seeded.WalletId)
            .Select(x => x.Balance)
            .SingleAsync());
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}

static Task TestStatisticalDistributionAsync()
{
    const int campaigns = 100;
    const int capacity = 2000;
    const int configuredPrizes = 200;
    const int userCount = 100;
    long aggregateWinners = 0;
    long aggregateRepeatedWinners = 0;
    var aggregateByUser = new int[userCount];

    for (var campaign = 0; campaign < campaigns; campaign++)
    {
        var random = new Random(1701 + campaign);
        var winnersByUser = new int[userCount];
        var winningUsers = new HashSet<int>();
        var assignedWinners = 0;

        for (var generated = 0; generated < capacity; generated++)
        {
            var userId = generated % userCount;
            var state = new ScratchcardDistributionState(
                generated,
                assignedWinners,
                Math.Min(userCount, generated + 1),
                winningUsers.Count,
                winnersByUser[userId],
                configuredPrizes - assignedWinners,
                capacity - generated);
            var probability = ScratchcardDistributionPolicy.CalculateWinnerProbability(
                1,
                10,
                state);
            if (!ScratchcardDistributionPolicy.IsWinner(
                    probability,
                    (decimal)random.NextDouble()))
                continue;

            assignedWinners++;
            winnersByUser[userId]++;
            aggregateByUser[userId]++;
            winningUsers.Add(userId);
        }

        aggregateWinners += assignedWinners;
        aggregateRepeatedWinners += winnersByUser.Count(x => x > 1);
        True(
            assignedWinners <= configuredPrizes,
            "La simulación nunca debe inventar premios.");
        True(
            winningUsers.Count >= 50,
            "La dispersión no debe concentrar artificialmente todos los premios.");
    }

    var averageWinners = (decimal)aggregateWinners / campaigns;
    True(
        averageWinners is >= 150m and <= 200m,
        $"La media estadística {averageWinners:F2} está claramente fuera del objetivo.");
    True(
        aggregateRepeatedWinners > 0,
        "Los usuarios deben conservar posibilidad real de ganar más de una vez.");
    True(
        aggregateByUser.All(x => x > 0),
        "Ningún usuario debe quedar bloqueado permanentemente.");

    Console.WriteLine(
        $"STAT campaigns={campaigns}, averageWinners={averageWinners:F2}, repeatedWinningUsers={aggregateRepeatedWinners}");
    return Task.CompletedTask;
}
static async Task TestStartScratchcardAsync()
{
    var databaseName = $"eLottoScratchcardTests_{Guid.NewGuid():N}";
    var options = CreateOptions(databaseName);
    try
    {
        var seeded = await RecreateAndSeedAsync(
            options,
            prizeQuantity: 5,
            initialBalance: 500m);
        var scratchcardId = await AddScratchcardAsync(
            options,
            seeded,
            isWinner: false);

        Equal<ScratchcardStartResponse>(
            null,
            await StartWithNewContextAsync(
                options,
                scratchcardId,
                seeded.UserId + 1000));

        var preview = await StartWithNewContextAsync(
            options,
            scratchcardId,
            seeded.UserId);
        Equal(scratchcardId, preview.Id);
        Equal(50m, preview.PremioPosible);
        Equal(9, preview.MatrizResultado.Count);

        await using (var verification = new eLottoContext(options))
        {
            var stored = await verification.SorteosRascaditos
                .AsNoTracking()
                .SingleAsync(x => x.Id == scratchcardId);
            True(!stored.Revelado, "Iniciar el raspado no debe marcar Revelado.");
            Equal<DateTime?>(null, stored.FechaRevelado);
            True(
                !string.IsNullOrWhiteSpace(stored.MatrizResultado),
                "La matriz visual debe quedar persistida desde el inicio.");
            Equal(
                500m,
                await verification.UserWallets
                    .Where(x => x.Id == seeded.WalletId)
                    .Select(x => x.Balance)
                    .SingleAsync());
            Equal(
                0,
                await verification.WalletTransactions.CountAsync(
                    x => x.Type == WalletTransactionType.ScratchcardPrize));
        }

        var revealed = await RevealWithNewContextAsync(
            options,
            scratchcardId,
            seeded.UserId);
        Equal(
            string.Join("|", preview.MatrizResultado),
            string.Join("|", revealed.MatrizResultado),
            "El revelado debe reutilizar exactamente la matriz preparada.");
        Equal(preview.PremioPosible, revealed.PremioPosible);
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}
static async Task TestRevealLosingScratchcardAsync()
{
    var databaseName = $"eLottoScratchcardTests_{Guid.NewGuid():N}";
    var options = CreateOptions(databaseName);
    try
    {
        var seeded = await RecreateAndSeedAsync(
            options,
            prizeQuantity: 5,
            initialBalance: 500m);
        var scratchcardId = await AddScratchcardAsync(
            options,
            seeded,
            isWinner: false);

        var response = await RevealWithNewContextAsync(
            options,
            scratchcardId,
            seeded.UserId);
        Equal(scratchcardId, response.Id);
        True(response.Revelado, "El perdedor debe quedar revelado.");
        True(!response.EsGanador, "El resultado persistido no debe cambiar.");
        True(response.ReveladoAhora, "El primer intento debe identificarse como revelado nuevo.");
        Equal<decimal?>(null, response.ImportePremio);
        Equal(50m, response.PremioPosible);
        Equal(500m, response.SaldoActual);
        Equal(9, response.MatrizResultado.Count);
        True(
            ScratchcardVisualResultGenerator.IsValid(
                new ScratchcardVisualResult(response.MatrizResultado, response.LineaGanadora),
                false),
            "El perdedor debe devolver una matriz persistida sin líneas.");

        await using var verification = new eLottoContext(options);
        var stored = await verification.SorteosRascaditos
            .AsNoTracking()
            .SingleAsync(x => x.Id == scratchcardId);
        True(stored.FechaRevelado.HasValue, "aebe registrarse FechaRevelado.");
        Equal(
            0,
            await verification.WalletTransactions.CountAsync(
                x => x.Type == WalletTransactionType.ScratchcardPrize));
        Equal(
            0,
            await verification.SorteosRascaditoPremios
                .Where(x => x.Id == seeded.PrizeId)
                .Select(x => x.Entregados)
                .SingleAsync());
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}

static async Task TestRevealWinningScratchcardAsync()
{
    var databaseName = $"eLottoScratchcardTests_{Guid.NewGuid():N}";
    var options = CreateOptions(databaseName);
    try
    {
        var seeded = await RecreateAndSeedAsync(
            options,
            prizeQuantity: 5,
            initialBalance: 500.10m);
        var scratchcardId = await AddScratchcardAsync(
            options,
            seeded,
            isWinner: true,
            prizeAmount: 100.25m);
        await using (var setup = new eLottoContext(options))
        {
            var sorteo = await setup.Sorteos.SingleAsync(x => x.Id == seeded.SorteoId);
            sorteo.ZonaHoraria = "Cancun";
            await setup.SaveChangesAsync();
        }

        await using (var beforeReveal = new eLottoContext(options))
        {
            Equal(
                500.10m,
                await beforeReveal.UserWallets
                    .Where(x => x.Id == seeded.WalletId)
                    .Select(x => x.Balance)
                    .SingleAsync());
            Equal(
                0,
                await beforeReveal.WalletTransactions.CountAsync(
                    x => x.Type == WalletTransactionType.ScratchcardPrize));
        }

        var serverBefore = ApplicationClock.NowOffset;
        var first = await RevealWithNewContextAsync(
            options,
            scratchcardId,
            seeded.UserId);
        var serverAfter = ApplicationClock.NowOffset;
        Equal(600.35m, first.SaldoActual);
        True(first.EsGanador, "El ganador debe conservar su resultado.");
        True(first.ReveladoAhora, "El primer revelado debe marcar el abono como nuevo.");
        Equal<decimal?>(100.25m, first.ImportePremio);
        Equal(100.25m, first.PremioPosible);
        Equal(9, first.MatrizResultado.Count);
        True(
            ScratchcardVisualResultGenerator.IsValid(
                new ScratchcardVisualResult(first.MatrizResultado, first.LineaGanadora),
                true),
            "El ganador debe devolver una línea real.");

        await using (var firstVerification = new eLottoContext(options))
        {
            var stored = await firstVerification.SorteosRascaditos
                .AsNoTracking()
                .SingleAsync(x => x.Id == scratchcardId);
            True(stored.WalletTransactionPremioId.HasValue, "aebe conservar la referencia directa al abono.");
            var movement = await firstVerification.WalletTransactions
                .AsNoTracking()
                .SingleAsync(x => x.Id == stored.WalletTransactionPremioId);
            Equal(WalletTransactionType.ScratchcardPrize, movement.Type);
            Equal(WalletTransactionStatus.Completed, movement.Status);
            Equal(100.25m, movement.Amount);
            Equal($"Premio de Rascadito {stored.Folio}", movement.Description);
            var sorteo = await firstVerification.Sorteos.AsNoTracking()
                .SingleAsync(x => x.Id == seeded.SorteoId);
            var timeService = new SorteoTimeService();
            var zoneBefore = timeService.ConvertToSorteoTime(serverBefore, sorteo).DateTime;
            var zoneAfter = timeService.ConvertToSorteoTime(serverAfter, sorteo).DateTime;
            True(stored.FechaRevelado >= zoneBefore && stored.FechaRevelado <= zoneAfter,
                "El revelado debe registrarse en la zona del sorteo.");
            Equal(stored.FechaRevelado, movement.CreatedAt);
            Equal(stored.FechaRevelado, movement.CompletedAt);
            Equal(seeded.SorteoId, movement.SorteoId);
            var wallet = await firstVerification.UserWallets.AsNoTracking()
                .SingleAsync(x => x.Id == seeded.WalletId);
            True(wallet.UpdatedAt >= serverBefore.DateTime &&
                wallet.UpdatedAt <= serverAfter.DateTime,
                "El saldo compartido debe conservar la hora local del servidor.");
        }

        var second = await RevealWithNewContextAsync(
            options,
            scratchcardId,
            seeded.UserId);
        Equal(600.35m, second.SaldoActual);
        Equal(first.FechaRevelado, second.FechaRevelado);
        True(!second.ReveladoAhora, "El reintento no debe simular un nuevo abono.");
        Equal(first.LineaGanadora, second.LineaGanadora);
        Equal(
            string.Join("|", first.MatrizResultado),
            string.Join("|", second.MatrizResultado),
            "La matriz persistida no debe regenerarse.");

        await using var finalVerification = new eLottoContext(options);
        Equal(
            1,
            await finalVerification.WalletTransactions.CountAsync(
                x => x.Type == WalletTransactionType.ScratchcardPrize));
        Equal(
            1,
            await finalVerification.SorteosRascaditoPremios
                .Where(x => x.Id == seeded.PrizeId)
                .Select(x => x.Entregados)
                .SingleAsync());
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}

static async Task TestConcurrentRevealAsync()
{
    var databaseName = $"eLottoScratchcardTests_{Guid.NewGuid():N}";
    var options = CreateOptions(databaseName);
    try
    {
        var seeded = await RecreateAndSeedAsync(
            options,
            prizeQuantity: 5,
            initialBalance: 500m);
        var scratchcardId = await AddScratchcardAsync(
            options,
            seeded,
            isWinner: true,
            prizeAmount: 100m);

        var start = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<ScratchcardRevealResponse> RevealConcurrentlyAsync()
        {
            await start.Task;
            return await RevealWithNewContextAsync(
                options,
                scratchcardId,
                seeded.UserId);
        }

        var revealTasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(RevealConcurrentlyAsync))
            .ToArray();
        start.SetResult();
        var responses = await Task.WhenAll(revealTasks);
        True(responses.All(x => x.SaldoActual == 600m), "Ambos reintentos deben observar el saldo final real.");

        await using var verification = new eLottoContext(options);
        Equal(
            600m,
            await verification.UserWallets
                .Where(x => x.Id == seeded.WalletId)
                .Select(x => x.Balance)
                .SingleAsync());
        Equal(
            1,
            await verification.WalletTransactions.CountAsync(
                x => x.Type == WalletTransactionType.ScratchcardPrize));
        Equal(
            1,
            await verification.SorteosRascaditoPremios
                .Where(x => x.Id == seeded.PrizeId)
                .Select(x => x.Entregados)
                .SingleAsync());
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}

static async Task TestRevealSecurityAndCorruptionAsync()
{
    var databaseName = $"eLottoScratchcardTests_{Guid.NewGuid():N}";
    var options = CreateOptions(databaseName);
    try
    {
        var seeded = await RecreateAndSeedAsync(
            options,
            prizeQuantity: 5,
            initialBalance: 500m);
        var losingScratchcardId = await AddScratchcardAsync(
            options,
            seeded,
            isWinner: false);

        Equal<ScratchcardRevealResponse>(
            null,
            await RevealWithNewContextAsync(
                options,
                losingScratchcardId,
                seeded.UserId + 1000));
        Equal<ScratchcardRevealResponse>(
            null,
            await RevealWithNewContextAsync(
                options,
                int.MaxValue,
                seeded.UserId));

        await using (var ownershipVerification = new eLottoContext(options))
        {
            var stored = await ownershipVerification.SorteosRascaditos
                .AsNoTracking()
                .SingleAsync(x => x.Id == losingScratchcardId);
            True(!stored.Revelado, "Un usuario ajeno no debe revelar el rascadito.");
            Equal(
                0,
                await ownershipVerification.WalletTransactions.CountAsync(
                    x => x.Type == WalletTransactionType.ScratchcardPrize));
        }

        var corruptScratchcardId = await AddScratchcardAsync(
            options,
            seeded,
            isWinner: true,
            prizeAmount: 100m);
        await using (var corruptionContext = new eLottoContext(options))
        {
            await corruptionContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE [SorteosRascaditos] NOCHECK CONSTRAINT [CK_SorteosRascaditos_Resultado]");
            await corruptionContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [SorteosRascaditos] SET [ImportePremio] = NULL WHERE [Id] = {corruptScratchcardId}");
        }

        await ThrowsAsync<ScratchcardDataIntegrityException>(
            () => RevealWithNewContextAsync(
                options,
                corruptScratchcardId,
                seeded.UserId));

        await using var finalVerification = new eLottoContext(options);
        var corrupt = await finalVerification.SorteosRascaditos
            .AsNoTracking()
            .SingleAsync(x => x.Id == corruptScratchcardId);
        True(!corrupt.Revelado, "La inconsistencia no debe dejar un estado parcial.");
        Equal(
            500m,
            await finalVerification.UserWallets
                .Where(x => x.Id == seeded.WalletId)
                .Select(x => x.Balance)
                .SingleAsync());
        Equal(
            0,
            await finalVerification.WalletTransactions.CountAsync(
                x => x.Type == WalletTransactionType.ScratchcardPrize));
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}

static async Task TestRevealInventoryIntegrityAsync()
{
    var databaseName = $"eLottoScratchcardTests_{Guid.NewGuid():N}";
    var options = CreateOptions(databaseName);
    try
    {
        var seeded = await RecreateAndSeedAsync(
            options,
            prizeQuantity: 5,
            initialBalance: 500m);
        var scratchcardId = await AddScratchcardAsync(
            options,
            seeded,
            isWinner: true,
            prizeAmount: 100m);

        await using (var corruption = new eLottoContext(options))
        {
            await corruption.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [SorteosRascaditoPremios] SET [Entregados] = 0 WHERE [Id] = {seeded.PrizeId}");
        }

        await ThrowsAsync<ScratchcardDataIntegrityException>(
            () => RevealWithNewContextAsync(
                options,
                scratchcardId,
                seeded.UserId));

        await using var verification = new eLottoContext(options);
        Equal(
            500m,
            await verification.UserWallets
                .Where(x => x.Id == seeded.WalletId)
                .Select(x => x.Balance)
                .SingleAsync());
        Equal(
            0,
            await verification.WalletTransactions.CountAsync(
                x => x.Type == WalletTransactionType.ScratchcardPrize));
        True(
            !await verification.SorteosRascaditos
                .Where(x => x.Id == scratchcardId)
                .Select(x => x.Revelado)
                .SingleAsync(),
            "El inventario inconsistente no debe dejar el rascadito revelado.");

        await using (var restore = new eLottoContext(options))
        {
            await restore.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [SorteosRascaditoPremios] SET [Entregados] = 1 WHERE [Id] = {seeded.PrizeId}");
        }

        var missingPrizeScratchcardId = await AddScratchcardAsync(
            options,
            seeded,
            isWinner: true,
            prizeAmount: 100m);
        await using (var missingPrize = new eLottoContext(options))
        {
            await missingPrize.Database.ExecuteSqlRawAsync(
                "ALTER TABLE [SorteosRascaditos] NOCHECK CONSTRAINT [FK_SorteosRascaditos_SorteosRascaditoPremios_SorteosRascaditoPremioId]");
            await missingPrize.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [SorteosRascaditos] SET [SorteosRascaditoPremioId] = {int.MaxValue} WHERE [Id] = {missingPrizeScratchcardId}");
        }

        await ThrowsAsync<ScratchcardDataIntegrityException>(
            () => RevealWithNewContextAsync(
                options,
                missingPrizeScratchcardId,
                seeded.UserId));
        await using var missingPrizeVerification = new eLottoContext(options);
        Equal(
            500m,
            await missingPrizeVerification.UserWallets
                .Where(x => x.Id == seeded.WalletId)
                .Select(x => x.Balance)
                .SingleAsync());
        Equal(
            0,
            await missingPrizeVerification.WalletTransactions.CountAsync(
                x => x.Type == WalletTransactionType.ScratchcardPrize));
    }
    finally
    {
        await DeleteDatabaseAsync(options);
    }
}
static DbContextOptions<eLottoContext> CreateOptions(string databaseName)
{
    var connectionString =
        $"Server=(localdb)\\MSSQLLocalDB;Database={databaseName};Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;Connect Timeout=15";
    return new DbContextOptionsBuilder<eLottoContext>()
        .UseSqlServer(connectionString, sql => sql.MigrationsAssembly("eLotto"))
        .Options;
}

static async Task<SeededData> RecreateAndSeedAsync(
    DbContextOptions<eLottoContext> options,
    int prizeQuantity,
    int winnersPerGroup = 1,
    int scratchcardsPerGroup = 10,
    decimal amountPerScratchcard = 30m,
    decimal depositAmount = 300m,
    decimal initialBalance = 0m)
{
    await using var context = new eLottoContext(options);
    await context.Database.EnsureDeletedAsync();
    await context.Database.EnsureCreatedAsync();

    var user = new Users
    {
        Name = "Usuario prueba",
        Email = "scratchcards@example.test",
        WhatsApp = "5216620000000",
        User = "scratchtest",
        Password = "not-used",
        IsActive = true,
        Date = ApplicationClock.Now,
        Languaje = "es"
    };
    context.Users.Add(user);
    await context.SaveChangesAsync();

    var wallet = new UserWallet
    {
        UserId = user.Id,
        Balance = initialBalance,
        CreatedAt = ApplicationClock.Now,
        UpdatedAt = ApplicationClock.Now
    };
    context.UserWallets.Add(wallet);

    var sorteo = new Sorteos
    {
        Nombre = "Sorteo prueba",
        Imagen1 = "test.webp",
        Fecha = ApplicationClock.Now.AddDays(1),
        PrecioBoleto = 1,
        PrecioPorMil = 1,
        CantidadBoletos = 100,
        PorcentajeMinimoVenta = 1,
        RascaditosHabilitados = true,
        GanadoresPorGrupo = winnersPerGroup,
        RascaditosPorGrupo = scratchcardsPerGroup,
        ImporteDepositoStripePorRascadito = amountPerScratchcard
    };
    var prize = new SorteosRascaditoPremios
    {
        Premio = 50m,
        Cantidad = prizeQuantity,
        Entregados = 0
    };
    sorteo.RascaditoPremios.Add(prize);
    context.Sorteos.Add(sorteo);
    await context.SaveChangesAsync();

    var paymentIntentId = $"pi_{Guid.NewGuid():N}";
    var transaction = new WalletTransaction
    {
        UserId = user.Id,
        WalletId = wallet.Id,
        SorteoId = sorteo.Id,
        Type = WalletTransactionType.Deposit,
        Amount = depositAmount,
        Status = WalletTransactionStatus.Pending,
        StripePaymentIntentId = paymentIntentId,
        Description = "aepósito de prueba",
        CreatedAt = ApplicationClock.Now
    };
    context.WalletTransactions.Add(transaction);
    await context.SaveChangesAsync();

    return new SeededData(
        user.Id,
        wallet.Id,
        sorteo.Id,
        prize.Id,
        transaction.Id,
        paymentIntentId);
}

static async Task<PendingDeposit> AddPendingDepositAsync(
    DbContextOptions<eLottoContext> options,
    int userId,
    int walletId,
    int sorteoId,
    decimal amount)
{
    await using var context = new eLottoContext(options);
    var paymentIntentId = $"pi_{Guid.NewGuid():N}";
    var transaction = new WalletTransaction
    {
        UserId = userId,
        WalletId = walletId,
        SorteoId = sorteoId,
        Type = WalletTransactionType.Deposit,
        Amount = amount,
        Status = WalletTransactionStatus.Pending,
        StripePaymentIntentId = paymentIntentId,
        Description = "aepósito concurrente de prueba",
        CreatedAt = ApplicationClock.Now
    };
    context.WalletTransactions.Add(transaction);
    await context.SaveChangesAsync();
    return new PendingDeposit(transaction.Id, paymentIntentId);
}

static async Task<ReferralDepositScenario> CreateReferralDepositScenarioAsync(
    DbContextOptions<eLottoContext> options,
    decimal depositAmount,
    decimal percentage,
    int maximumRewards,
    bool isActive = true,
    bool includeSettings = true,
    bool createReferrerWallet = false,
    bool referrerIsActive = true,
    decimal referrerBalance = 0m,
    decimal referredBalance = 0m)
{
    var deposit = await RecreateAndSeedAsync(
        options,
        prizeQuantity: 1,
        depositAmount: depositAmount);

    await using var context = new eLottoContext(options);
    var lottery = await context.Sorteos.SingleAsync(x => x.Id == deposit.SorteoId);
    lottery.RascaditosHabilitados = false;
    lottery.GanadoresPorGrupo = 0;
    lottery.RascaditosPorGrupo = 0;
    lottery.ImporteDepositoStripePorRascadito = 0m;

    var referrer = new Users
    {
        Name = "Usuario referidor",
        Email = "referrer@example.test",
        WhatsApp = "5216622000001",
        User = "referrer",
        Password = "not-used",
        IsActive = referrerIsActive,
        Date = ApplicationClock.Now,
        Languaje = "es",
        ReferralCode = "REFERR01"
    };
    context.Users.Add(referrer);
    var referred = await context.Users.SingleAsync(x => x.Id == deposit.UserId);
    referred.Referrer = referrer;
    var referredWallet = await context.UserWallets.SingleAsync(x => x.Id == deposit.WalletId);
    referredWallet.Balance = referredBalance;

    if (includeSettings)
    {
        context.ReferralProgramSettings.Add(new ReferralProgramSettings
        {
            Id = 1,
            IsActive = isActive,
            DepositRewardPercentage = percentage,
            MaxRewardedDeposits = maximumRewards,
            WinnerCashRewardAmount = 5000m,
            MinimumConfirmedTickets = 100,
            UpdatedAt = ApplicationClock.Now
        });
    }

    UserWallet referrerWallet = null;
    if (createReferrerWallet)
    {
        referrerWallet = new UserWallet
        {
            User = referrer,
            Balance = referrerBalance,
            CreatedAt = ApplicationClock.Now,
            UpdatedAt = ApplicationClock.Now
        };
        context.UserWallets.Add(referrerWallet);
    }

    await context.SaveChangesAsync();
    return new ReferralDepositScenario(deposit, referrer.Id, referrerWallet?.Id);
}

static async Task<ReferredDeposit> AddReferredUserDepositAsync(
    DbContextOptions<eLottoContext> options,
    int referrerUserId,
    int sorteoId,
    decimal amount,
    string referralCode)
{
    await using var context = new eLottoContext(options);
    var suffix = Guid.NewGuid().ToString("N")[..8];
    var user = new Users
    {
        Name = "Segundo referido",
        Email = $"{suffix}@example.test",
        WhatsApp = $"521{RandomNumberGenerator.GetInt32(1000000000, int.MaxValue)}",
        User = $"user{suffix}",
        Password = "not-used",
        IsActive = true,
        Date = ApplicationClock.Now,
        Languaje = "es",
        ReferralCode = referralCode,
        ReferredByUserId = referrerUserId
    };
    context.Users.Add(user);
    await context.SaveChangesAsync();

    var wallet = new UserWallet
    {
        UserId = user.Id,
        Balance = 0m,
        CreatedAt = ApplicationClock.Now,
        UpdatedAt = ApplicationClock.Now
    };
    context.UserWallets.Add(wallet);
    await context.SaveChangesAsync();

    var paymentIntentId = $"pi_{Guid.NewGuid():N}";
    var transaction = new WalletTransaction
    {
        UserId = user.Id,
        WalletId = wallet.Id,
        SorteoId = sorteoId,
        Type = WalletTransactionType.Deposit,
        Amount = amount,
        Status = WalletTransactionStatus.Pending,
        StripePaymentIntentId = paymentIntentId,
        Description = "Depósito referido de prueba",
        CreatedAt = ApplicationClock.Now
    };
    context.WalletTransactions.Add(transaction);
    await context.SaveChangesAsync();
    return new ReferredDeposit(user.Id, wallet.Id, transaction.Id, paymentIntentId);
}

static async Task UpdateReferralSettingsAsync(
    DbContextOptions<eLottoContext> options,
    bool isActive,
    decimal percentage,
    int maximumRewards)
{
    await using var context = new eLottoContext(options);
    var settings = await context.ReferralProgramSettings.SingleAsync(x => x.Id == 1);
    settings.IsActive = isActive;
    settings.DepositRewardPercentage = percentage;
    settings.MaxRewardedDeposits = maximumRewards;
    settings.UpdatedAt = ApplicationClock.Now;
    await context.SaveChangesAsync();
}

static async Task AssertReferralRewardCountAsync(
    DbContextOptions<eLottoContext> options,
    int referredUserId,
    int expected)
{
    await using var context = new eLottoContext(options);
    Equal(expected, await context.ReferralDepositRewards.CountAsync(
        x => x.ReferredUserId == referredUserId));
}

static async Task<ReferralWinnerScenario> CreateReferralWinnerScenarioAsync(
    DbContextOptions<eLottoContext> options,
    int minimumTickets,
    int actualTickets,
    decimal rewardAmount,
    bool isActive = true,
    bool includeSettings = true,
    bool referrerIsActive = true,
    int otherLotteryTickets = 0,
    string zonaHoraria = "CDMX")
{
    var seeded = await RecreateAndSeedAsync(options, prizeQuantity: 1);
    await using var context = new eLottoContext(options);
    var lottery = await context.Sorteos.SingleAsync(x => x.Id == seeded.SorteoId);
    lottery.ZonaHoraria = zonaHoraria;
    lottery.Fecha = new SorteoTimeService().ConvertToSorteoTime(
        ApplicationClock.NowOffset.AddHours(-1), lottery).DateTime;

    var referrer = new Users
    {
        Name = "Referidor del ganador",
        Email = "winner-referrer@example.test",
        WhatsApp = "5216623000001",
        User = "winnerreferrer",
        Password = "not-used",
        IsActive = referrerIsActive,
        Date = ApplicationClock.Now,
        Languaje = "es",
        ReferralCode = "WINREF01"
    };
    context.Users.Add(referrer);
    var winner = await context.Users.SingleAsync(x => x.Id == seeded.UserId);
    winner.Referrer = referrer;

    if (includeSettings)
        context.ReferralProgramSettings.Add(CreateReferralSettings(
            isActive,
            rewardAmount,
            minimumTickets));

    await context.SaveChangesAsync();

    context.BoletosConfirmados.Add(new BoletosConfirmados
    {
        SorteosId = seeded.SorteoId,
        Numero = "042",
        FolioCompra = "WINNER01",
        UsuarioId = winner.Id,
        Fecha = ApplicationClock.Now.AddMinutes(-30),
        UsuarioIdConfirm = winner.Id,
        WhatsAppConfirm = winner.WhatsApp,
        CuentaAsignada = string.Empty
    });
    AddReferrerTickets(
        context,
        seeded.SorteoId,
        referrer,
        actualTickets,
        100);

    await context.SaveChangesAsync();

    if (otherLotteryTickets > 0)
    {
        var otherLottery = new Sorteos
        {
            Nombre = "Sorteo ajeno al premio",
            Imagen1 = "other.webp",
            Fecha = ApplicationClock.Now.AddDays(5),
            PrecioBoleto = 1m,
            PrecioPorMil = 1m,
            CantidadBoletos = 100
        };
        context.Sorteos.Add(otherLottery);
        await context.SaveChangesAsync();
        AddReferrerTickets(
            context,
            otherLottery.Id,
            referrer,
            otherLotteryTickets,
            200);
        await context.SaveChangesAsync();
    }

    await AddPublishedTransparencyFixtureAsync(context, seeded.SorteoId);

    return new ReferralWinnerScenario(
        seeded.SorteoId,
        winner.Id,
        referrer.Id);
}

static async Task<int> AddLotteryForReferralWinnerAsync(
    DbContextOptions<eLottoContext> options,
    int winnerUserId,
    int referrerUserId,
    int actualReferrerTickets,
    string name)
{
    await using var context = new eLottoContext(options);
    var lottery = new Sorteos
    {
        Nombre = name,
        Imagen1 = "referral-winner.webp",
        Fecha = ApplicationClock.Now.AddHours(-1),
        PrecioBoleto = 1m,
        PrecioPorMil = 1m,
        CantidadBoletos = 100,
        PorcentajeMinimoVenta = 1
    };
    context.Sorteos.Add(lottery);
    await context.SaveChangesAsync();

    var winner = await context.Users.SingleAsync(x => x.Id == winnerUserId);
    var referrer = await context.Users.SingleAsync(x => x.Id == referrerUserId);
    context.BoletosConfirmados.Add(new BoletosConfirmados
    {
        SorteosId = lottery.Id,
        Numero = "042",
        FolioCompra = $"WIN{lottery.Id:D5}",
        UsuarioId = winner.Id,
        Fecha = ApplicationClock.Now.AddMinutes(-30),
        UsuarioIdConfirm = winner.Id,
        WhatsAppConfirm = winner.WhatsApp,
        CuentaAsignada = string.Empty
    });
    AddReferrerTickets(
        context,
        lottery.Id,
        referrer,
        actualReferrerTickets,
        100);
    await context.SaveChangesAsync();
    await AddPublishedTransparencyFixtureAsync(context, lottery.Id);
    return lottery.Id;
}

static async Task AddPublishedTransparencyFixtureAsync(eLottoContext context, int sorteoId)
{
    var lottery = await context.Sorteos.SingleAsync(x => x.Id == sorteoId);
    var files = new SorteoTransparencyFileStore(SorteoTransparencyFileStore.DefaultDirectory);
    await files.WriteOnceAsync(lottery, "%PDF-1.4 test fixture"u8.ToArray(), CancellationToken.None);
}

static void AddReferrerTickets(
    eLottoContext context,
    int sorteoId,
    Users referrer,
    int quantity,
    int firstNumber)
{
    for (var index = 0; index < quantity; index++)
    {
        context.BoletosConfirmados.Add(new BoletosConfirmados
        {
            SorteosId = sorteoId,
            Numero = (firstNumber + index).ToString("D3"),
            FolioCompra = index < Math.Max(1, quantity / 2)
                ? $"REFA{sorteoId:D5}"
                : $"REFB{sorteoId:D5}",
            UsuarioId = referrer.Id,
            Fecha = ApplicationClock.Now.AddMinutes(-20 + index),
            UsuarioIdConfirm = referrer.Id,
            WhatsAppConfirm = referrer.WhatsApp,
            CuentaAsignada = string.Empty
        });
    }
}

static ReferralProgramSettings CreateReferralSettings(
    bool isActive,
    decimal winnerReward,
    int minimumTickets) =>
    new()
    {
        Id = 1,
        IsActive = isActive,
        DepositRewardPercentage = 10m,
        MaxRewardedDeposits = 5,
        WinnerCashRewardAmount = winnerReward,
        MinimumConfirmedTickets = minimumTickets,
        UpdatedAt = ApplicationClock.Now
    };

static async Task UpdateWinnerReferralSettingsAsync(
    DbContextOptions<eLottoContext> options,
    bool isActive,
    decimal rewardAmount,
    int minimumTickets)
{
    await using var context = new eLottoContext(options);
    var settings = await context.ReferralProgramSettings.SingleAsync(x => x.Id == 1);
    settings.IsActive = isActive;
    settings.WinnerCashRewardAmount = rewardAmount;
    settings.MinimumConfirmedTickets = minimumTickets;
    settings.UpdatedAt = ApplicationClock.Now;
    await context.SaveChangesAsync();
}

static async Task FinalizeLotteryAsync(
    DbContextOptions<eLottoContext> options,
    int sorteoId)
{
    await using var context = new eLottoContext(options);
    var lottery = await context.Sorteos.AsNoTracking().SingleAsync(x => x.Id == sorteoId);
    if (!new SorteoTransparencyFileStore(SorteoTransparencyFileStore.DefaultDirectory).Exists(lottery))
        await AddPublishedTransparencyFixtureAsync(context, sorteoId);
    var result = await new SorteosRepository(context).FinalizeWinnerAsync(
        sorteoId,
        "42",
        CancellationToken.None);
    Equal(SorteoResultadoStatus.Ok, result.Status);
}

static async Task AssertWinnerCashRewardCountAsync(
    DbContextOptions<eLottoContext> options,
    int expected)
{
    await using var context = new eLottoContext(options);
    Equal(expected, await context.ReferralWinnerCashRewards.CountAsync());
}

static async Task<int> AddScratchcardAsync(
    DbContextOptions<eLottoContext> options,
    SeededData seeded,
    bool isWinner,
    decimal? prizeAmount = null)
{
    await using var context = new eLottoContext(options);
    if (isWinner)
    {
        var prize = await context.SorteosRascaditoPremios
            .SingleAsync(x => x.Id == seeded.PrizeId);
        if (prizeAmount.HasValue) prize.Premio = prizeAmount.Value;
        prize.Entregados++;
    }

    var scratchcard = new SorteosRascaditos
    {
        SorteosId = seeded.SorteoId,
        UsuarioId = seeded.UserId,
        WalletTransactionOrigenId = seeded.TransactionId,
        Folio = ScratchcardFolioGenerator.Create(),
        EsGanador = isWinner,
        SorteosRascaditoPremioId = isWinner ? seeded.PrizeId : null,
        ImportePremio = isWinner ? prizeAmount : null,
        Revelado = false,
        FechaGeneracion = ApplicationClock.Now
    };
    context.SorteosRascaditos.Add(scratchcard);
    await context.SaveChangesAsync();
    return scratchcard.Id;
}

static async Task<ScratchcardStartResponse> StartWithNewContextAsync(
    DbContextOptions<eLottoContext> options,
    int scratchcardId,
    int userId)
{
    await using var context = new eLottoContext(options);
    var service = new ScratchcardRevealService(
        context,
        NullLogger<ScratchcardRevealService>.Instance,
        new SorteoTimeService());
    return await service.StartAsync(
        scratchcardId,
        userId,
        CancellationToken.None);
}
static async Task<ScratchcardRevealResponse> RevealWithNewContextAsync(
    DbContextOptions<eLottoContext> options,
    int scratchcardId,
    int userId)
{
    await using var context = new eLottoContext(options);
    var service = new ScratchcardRevealService(
        context,
        NullLogger<ScratchcardRevealService>.Instance,
        new SorteoTimeService());
    return await service.RevealAsync(
        scratchcardId,
        userId,
        CancellationToken.None);
}

static SorteosRascaditos CreateLosingScratchcard(SeededData seeded, string folio) =>
    new()
    {
        SorteosId = seeded.SorteoId,
        UsuarioId = seeded.UserId,
        WalletTransactionOrigenId = seeded.TransactionId,
        Folio = folio,
        EsGanador = false,
        Revelado = false,
        FechaGeneracion = ApplicationClock.Now
    };

static Event CreateSucceededEvent(
    string eventId,
    string paymentIntentId,
    int transactionId,
    int userId,
    decimal amount)
{
    var minorUnits = checked((long)(amount * 100m));
    return new Event
    {
        Id = eventId,
        Object = "event",
        Type = "payment_intent.succeeded",
        Data = new EventData
        {
            Object = new PaymentIntent
            {
                Id = paymentIntentId,
                Object = "payment_intent",
                Amount = minorUnits,
                AmountReceived = minorUnits,
                Currency = "mxn",
                Metadata = new Dictionary<string, string>
                {
                    ["Application"] = "Sorteos Global Broker",
                    ["WalletTransactionId"] = transactionId.ToString(),
                    ["UserId"] = userId.ToString()
                }
            }
        }
    };
}

static async Task ProcessEventAsync(
    DbContextOptions<eLottoContext> options,
    Event stripeEvent,
    ILogger<WalletPaymentService> logger = null)
{
    await using var context = new eLottoContext(options);
    var repository = new UserLotteryRepository(context);
    var scratchcards = new ScratchcardAssignmentService(
        context,
        NullLogger<ScratchcardAssignmentService>.Instance,
        new SorteoTimeService());
    var configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string>
        {
            ["AppUrls:PublicApiBaseUrl"] = "https://localhost",
            ["AppUrls:FrontendBaseUrl"] = "http://localhost:4200",
            ["Branding:ApplicationName"] = "Sorteos Global Broker"
        })
        .Build();
    var service = new WalletPaymentService(
        context,
        Options.Create(new StripeOptions()),
        Options.Create(new DepositOptions { Currency = "mxn" }),
        logger ?? NullLogger<WalletPaymentService>.Instance,
        new NullWhatsAppService(),
        scratchcards,
        repository,
        configuration,
        new SorteoTimeService());

    await service.ProcessStripeEventAsync(stripeEvent);
}

static async Task<ScratchcardResult> ReadResultAsync(
    DbContextOptions<eLottoContext> options,
    SeededData seeded)
{
    await using var context = new eLottoContext(options);
    var scratchcards = await context.SorteosRascaditos
        .AsNoTracking()
        .Where(x => x.WalletTransactionOrigenId == seeded.TransactionId)
        .ToListAsync();
    var delivered = await context.SorteosRascaditoPremios
        .AsNoTracking()
        .Where(x => x.SorteosId == seeded.SorteoId)
        .SumAsync(x => x.Entregados);
    var wallet = await context.UserWallets.AsNoTracking()
        .SingleAsync(x => x.Id == seeded.WalletId);
    var transaction = await context.WalletTransactions.AsNoTracking()
        .SingleAsync(x => x.Id == seeded.TransactionId);

    return new ScratchcardResult(
        scratchcards.Count,
        scratchcards.Count(x => x.EsGanador),
        delivered,
        wallet.Balance,
        transaction.Status,
        scratchcards.All(x => !x.Revelado && x.FechaRevelado == null),
        scratchcards.All(x =>
            x.EsGanador
                ? x.SorteosRascaditoPremioId.HasValue && x.ImportePremio > 0
                : !x.SorteosRascaditoPremioId.HasValue && !x.ImportePremio.HasValue));
}

static string FindFolioForWeightedPrize(
    IReadOnlyList<int> weights,
    int desiredIndex)
{
    var totalWeight = weights.Sum(weight => (long)weight);
    if (desiredIndex < 0 || desiredIndex >= weights.Count || totalWeight <= 0)
        throw new ArgumentOutOfRangeException(nameof(desiredIndex));

    var lowerBound = weights.Take(desiredIndex).Sum(weight => (long)weight);
    var upperBound = lowerBound + weights[desiredIndex];
    for (var attempt = 0; attempt < 10000; attempt++)
    {
        var folio = $"VISUAL{attempt:D8}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(folio));
        var ticket = (long)(
            BinaryPrimitives.ReadUInt64LittleEndian(hash) %
            (ulong)totalWeight);
        if (ticket >= lowerBound && ticket < upperBound) return folio;
    }

    throw new InvalidOperationException("No fue posible crear un folio para el premio visual esperado.");
}

static async Task DeleteDatabaseAsync(DbContextOptions<eLottoContext> options)
{
    await using var context = new eLottoContext(options);
    await context.Database.EnsureDeletedAsync();
}

static async Task ThrowsAsync<TException>(Func<Task> action)
    where TException : Exception
{
    try
    {
        await action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException(
        $"Se esperaba una excepción {typeof(TException).Name}.");
}

static void Equal<T>(T expected, T actual, string message = null)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException(
            message ?? $"Se esperaba {expected}, se obtuvo {actual}.");
}

static void True(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

sealed class NullConfirmedTicketDeliveryService : IConfirmedTicketDeliveryService
{
    public Task DeliverAsync(int sorteoId, int userId, string folio, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}

sealed record PendingDeposit(
    int TransactionId,
    string PaymentIntentId);

sealed record ReferralDepositScenario(
    SeededData Deposit,
    int ReferrerUserId,
    int? ReferrerWalletId);

sealed record ReferredDeposit(
    int UserId,
    int WalletId,
    int TransactionId,
    string PaymentIntentId);

sealed record ReferralWinnerScenario(
    int SorteoId,
    int WinnerUserId,
    int ReferrerUserId);

sealed record SeededData(
    int UserId,
    int WalletId,
    int SorteoId,
    int PrizeId,
    int TransactionId,
    string PaymentIntentId);

sealed record ScratchcardResult(
    int Scratchcards,
    int Winners,
    int Delivered,
    decimal Balance,
    WalletTransactionStatus Status,
    bool AllUnrevealed,
    bool AllResultsCoherent);

sealed class NullWhatsAppService : IWhatsAppService
{
    public Task<bool> SendTextAsync(string whatsApp, string message) =>
        Task.FromResult(false);

    public Task<bool> SendDocumentAsync(
        string whatsApp,
        string fileName,
        byte[] document,
        string caption,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}

sealed class SuccessfulWhatsAppService : IWhatsAppService
{
    public Task<bool> SendTextAsync(string whatsApp, string message) => Task.FromResult(true);

    public Task<bool> SendDocumentAsync(
        string whatsApp,
        string fileName,
        byte[] document,
        string caption,
        CancellationToken cancellationToken = default) => Task.FromResult(true);
}

sealed class CollectingWalletLogger : ILogger<WalletPaymentService>
{
    public List<LogLevel> Levels { get; } = [];

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull =>
        EmptyScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception exception,
        Func<TState, Exception, string> formatter)
    {
        Levels.Add(logLevel);
    }

    private sealed class EmptyScope : IDisposable
    {
        public static EmptyScope Instance { get; } = new();
        public void Dispose() { }
    }
}

sealed class SequentialReferralCodeGenerator(params string[] codes) : IReferralCodeGenerator
{
    private int _index;

    public int Calls => _index;

    public string Generate()
    {
        if (_index >= codes.Length)
            throw new InvalidOperationException("No quedan códigos de prueba configurados.");

        return codes[_index++];
    }
}

