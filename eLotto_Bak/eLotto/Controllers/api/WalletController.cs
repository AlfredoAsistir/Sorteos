using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using eLotto.Services;

namespace eLotto.Controllers.api
{
    [ApiController]
    [Authorize]
    [Route("api/wallet")]
    public class WalletController : ControllerBase
    {
        private readonly IWalletPaymentService _walletPaymentService;
        public WalletController(IWalletPaymentService walletPaymentService) => _walletPaymentService = walletPaymentService;

        [HttpGet]
        public async Task<IActionResult> GetWallet()
        {
            try { return Ok(await _walletPaymentService.GetWalletAsync(User.Identity?.Name)); }
            catch (UnauthorizedAccessException) { return Unauthorized(); }
        }

        [HttpGet("transactions")]
        public async Task<IActionResult> GetTransactions([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            try { return Ok(await _walletPaymentService.GetTransactionsAsync(User.Identity?.Name, page, pageSize)); }
            catch (UnauthorizedAccessException) { return Unauthorized(); }
        }
    }
}
