using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using eLotto.Models;
using eLotto.Options;
using eLotto.Services;
using Stripe;

namespace eLotto.Controllers.api
{
    [ApiController]
    [Route("api/payments")]
    public class PaymentsController : ControllerBase
    {
        private readonly IWalletPaymentService _walletPaymentService;
        private readonly StripeOptions _stripeOptions;
        private readonly ILogger<PaymentsController> _logger;
        private readonly string _frontendBaseUrl;

        public PaymentsController(IWalletPaymentService walletPaymentService,
            IOptions<StripeOptions> stripeOptions, ILogger<PaymentsController> logger,
            IConfiguration configuration)
        {
            _walletPaymentService = walletPaymentService;
            _stripeOptions = stripeOptions.Value;
            _logger = logger;
            _frontendBaseUrl = configuration["AppUrls:FrontendBaseUrl"]?.TrimEnd('/')
                ?? throw new InvalidOperationException("AppUrls:FrontendBaseUrl is not configured.");
        }

        [Authorize]
        [HttpPost("deposits/payment-intent")]
        public async Task<IActionResult> CreateDepositPaymentIntent([FromBody] CreateDepositPaymentIntentRequest request)
        {
            try
            {
                return Ok(await _walletPaymentService.CreateDepositAsync(
                    User.Identity?.Name,
                    request.SorteoId,
                    request.Amount,
                    request.PaymentMethod,
                    request.Name,
                    request.Email));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized();
            }
            catch (StripeException)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new { message = "No fue posible iniciar el pago con Stripe. Inténtalo nuevamente." });
            }
        }

        [Authorize]

        [HttpPost("deposits/{transactionId:int}/sync")]
        public async Task<IActionResult> SyncDepositStatus(int transactionId)
        {
            try
            {
                return Ok(await _walletPaymentService.SyncDepositStatusAsync(User.Identity?.Name, transactionId));
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized();
            }
        }

        [AllowAnonymous]
        [HttpGet("/p/{code}")]
        public async Task<IActionResult> OpenPaymentInstructions(string code)
        {
            Response.Headers.CacheControl = "no-store";
            Response.Headers.Append("Referrer-Policy", "no-referrer");
            try
            {
                var instructionUrl = await _walletPaymentService.ResolveInstructionUrlAsync(code);
                if (instructionUrl == null)
                    return Redirect($"{_frontendBaseUrl}/p/estado/no-disponible");

                return Redirect(instructionUrl);
            }
            catch (StripeException ex)
            {
                _logger.LogWarning(ex, "Could not resolve payment instruction short link.");
                return Redirect($"{_frontendBaseUrl}/p/estado/error");
            }
        }
        [AllowAnonymous]
        [HttpPost("stripe/webhook")]
        public async Task<IActionResult> StripeWebhook()
        {
            if (string.IsNullOrWhiteSpace(_stripeOptions.WebhookSecret) ||
                !_stripeOptions.WebhookSecret.StartsWith("whsec_", StringComparison.Ordinal))
            {
                _logger.LogError("Stripe webhook secret is not configured.");
                return StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            var json = await new StreamReader(Request.Body).ReadToEndAsync();
            try
            {
                var stripeEvent = EventUtility.ConstructEvent(json, Request.Headers["Stripe-Signature"], _stripeOptions.WebhookSecret);
                await _walletPaymentService.ProcessStripeEventAsync(
                    stripeEvent,
                    HttpContext.RequestAborted);
                return Ok();
            }
            catch (StripeException ex)
            {
                _logger.LogWarning(ex, "Stripe webhook signature validation failed.");
                return BadRequest();
            }
        }
    }
}
